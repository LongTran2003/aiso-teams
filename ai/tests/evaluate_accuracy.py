import json
import os
import sys
import time
import argparse
import re
from pathlib import Path
from dotenv import load_dotenv
from openai import OpenAI
import openai

# Force standard output to use UTF-8 encoding (especially on Windows)
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")

# ---------------------------------------------------------------------------
# Setup and Configuration
# ---------------------------------------------------------------------------

# Resolve paths relative to this script
SCRIPT_DIR = Path(__file__).resolve().parent
AI_DIR = SCRIPT_DIR.parent
ENV_PATH = AI_DIR / ".env"
FUNCTIONS_DIR = AI_DIR / "functions"
# Eval uses the condensed prompt (no few-shot) to stay within llama-3.1-8b-instant
# 6K TPM limit. Production uses the full system_prompt.txt with few-shot examples.
SYSTEM_PROMPT_PATH = AI_DIR / "prompts" / "system_prompt_eval.txt"
SYSTEM_PROMPT_FALLBACK_PATH = AI_DIR / "prompts" / "system_prompt.txt"
GOLDEN_DATA_PATH = SCRIPT_DIR / "golden.jsonl"

# Load environment variables
if ENV_PATH.exists():
    load_dotenv(dotenv_path=ENV_PATH)
else:
    load_dotenv()  # Fallback to default loading

GROQ_API_KEY = os.getenv("GROQ_API_KEY", "")

# ---------------------------------------------------------------------------
# Schema and Tool Loading
# ---------------------------------------------------------------------------


def _load_system_prompt() -> str:
    """
    Load condensed eval prompt (system_prompt_eval.txt) to fit within
    llama-3.1-8b-instant 6K TPM limit. Falls back to full system_prompt.txt
    if the eval file is missing.
    """
    for path in (SYSTEM_PROMPT_PATH, SYSTEM_PROMPT_FALLBACK_PATH):
        try:
            content = path.read_text(encoding="utf-8").strip()
            label = path.name
            print(f"[Info] Loaded system prompt: {label} ({len(content)} chars)", flush=True)
            return content
        except FileNotFoundError:
            continue
    return "Bạn là trợ lý AI xử lý hệ thống SAP. Hãy đọc câu hỏi và trả về dữ liệu định dạng JSON để hệ thống gọi hàm."


def load_tools() -> list[dict]:
    """Load all JSON function schemas from the functions directory in OpenAI tool format."""
    if not FUNCTIONS_DIR.exists():
        print(
            f"Error: Functions directory not found at {FUNCTIONS_DIR}", file=sys.stderr
        )
        sys.exit(1)

    tools = []
    for path in sorted(FUNCTIONS_DIR.glob("*.json")):
        try:
            raw = json.loads(path.read_text(encoding="utf-8"))
            # Format as OpenAI function tool
            if "type" in raw and raw["type"] == "function":
                tool = raw
            else:
                tool = {
                    "type": "function",
                    "function": {
                        "name": raw["name"],
                        "description": raw.get("description", ""),
                        "parameters": raw.get("parameters", {}),
                    },
                }
            tools.append(tool)
        except Exception as exc:
            print(f"Error loading schema {path.name}: {exc}", file=sys.stderr)

    return tools


# ---------------------------------------------------------------------------
# API Query with Retry
# ---------------------------------------------------------------------------


def generate_content_with_retry(
    client, model, messages, tools, max_retries=6, initial_backoff=3
):
    """
    Calls the OpenAI client with robust retries, handling rate limits and connection errors.
    If it fails after max_retries, it raises the last exception.
    """
    last_exc = None
    for attempt in range(max_retries):
        try:
            kwargs = {
                "model": model,
                "messages": messages,
                "temperature": 0.1,
            }
            if tools:
                kwargs["tools"] = tools
                kwargs["tool_choice"] = "auto"

            return client.chat.completions.create(**kwargs)
        except openai.RateLimitError as exc:
            last_exc = exc
            sleep_time = initial_backoff * (2**attempt)
            print(
                f"\n[Warning] Groq Rate limit hit. Waiting {sleep_time:.2f} seconds before retrying...",
                flush=True,
            )
            time.sleep(sleep_time)
        except openai.APIConnectionError as exc:
            last_exc = exc
            sleep_time = initial_backoff * (2**attempt)
            print(
                f"\n[Warning] Connection Error. Waiting {sleep_time:.2f} seconds before retrying...",
                flush=True,
            )
            time.sleep(sleep_time)
        except openai.AuthenticationError as exc:
            print(f"\n[Error] Invalid Groq API Key: {exc}", file=sys.stderr)
            raise exc
        except openai.BadRequestError as exc:
            raise exc
        except openai.APIStatusError as exc:
            if exc.status_code == 413:
                # Request too large – cannot retry, skip this query
                print(
                    f"\n[Error] Groq API error status 413: {exc}",
                    file=sys.stderr,
                )
                raise exc
            elif exc.status_code >= 500:
                last_exc = exc
                sleep_time = initial_backoff * (2**attempt)
                print(
                    f"\n[Warning] Groq Server Error ({exc.status_code}). Waiting {sleep_time:.2f} seconds before retrying...",
                    flush=True,
                )
                time.sleep(sleep_time)
            else:
                print(
                    f"\n[Error] Groq API error status {exc.status_code}: {exc}",
                    file=sys.stderr,
                )
                raise exc
        except Exception as exc:
            raise exc

    if last_exc:
        raise last_exc
    raise Exception("API call failed after max retries without specific exception.")


def validate_parameters(
    fn_name: str, args: dict, user_message: str
) -> tuple[str, dict] | None:
    """
    Validates parameters based on logic rules:
    - Hallucinated order_id detection (must be present in user_message).
    - Hallucinated reason_code detection (must be inferable from user_message).
    - Required parameters checks.
    - Null-cleaning + hallucination-stripping for optional-only param functions.
    Returns (fn_name, args) if valid, or None if invalid.
    """
    lower_msg = user_message.lower()
    order_id = args.get("order_id")

    # 1. Hallucinated order_id check
    if order_id and str(order_id).lower() not in lower_msg:
        return None

    # 2. Hallucinated forward_to_user check
    forward_to_user = args.get("forward_to_user")
    if forward_to_user and str(forward_to_user).lower() not in lower_msg:
        return None

    # 3. Hallucinated reason_code check for RejectOrder
    # reason_code must be inferable from the user message; never guessed silently.
    if fn_name == "RejectOrder":
        reason_code = args.get("reason_code")
        if reason_code and str(reason_code).lower() not in ("null", ""):
            REASON_KEYWORDS = {
                "PRICE_ISSUE": ["giá", "price", "sai giá", "pricing", "giá cả"],
                "OUT_OF_STOCK": ["hết hàng", "out of stock", "hết", "stock"],
                "OTHER": ["khác", "other reason", "lý do khác"],
            }
            expected_keywords = REASON_KEYWORDS.get(str(reason_code).upper(), [])
            if not any(kw in lower_msg for kw in expected_keywords):
                return None  # reason_code was guessed; should have asked user

    # 4. Schema rule checks (required fields)
    if fn_name == "CheckOrderStatus":
        if not order_id or order_id == "null" or str(order_id).strip() == "":
            return None
    elif fn_name == "ReleaseOrder":
        if not order_id or order_id == "null" or str(order_id).strip() == "":
            return None
        # Clean empty optional string fields and 'null' strings
        args = {k: v for k, v in args.items()
                if not (isinstance(v, str) and (v.strip() == "" or v.strip().lower() == "null") and k != "order_id")}
    elif fn_name == "RejectOrder":
        reason_code = args.get("reason_code")
        if not order_id or order_id == "null" or str(order_id).strip() == "":
            return None
        if not reason_code or reason_code == "null" or str(reason_code).strip() == "":
            return None
    elif fn_name == "ForwardOrder":
        forward_to_user = args.get("forward_to_user")
        if not order_id or order_id == "null" or str(order_id).strip() == "":
            return None
        if (
            not forward_to_user
            or forward_to_user == "null"
            or str(forward_to_user).strip() == ""
        ):
            return None

    # 5. Smart hallucination-stripping for optional-only param functions
    NO_REQUIRED_FUNCS = {
        "GetSalesOrders", "GetKpiSummary", "GetKpiByCustomer",
        "GetKpiByProduct", "GetOverdueOrders",
    }
    if fn_name in NO_REQUIRED_FUNCS:
        cleaned = {}
        # Keywords that indicate user explicitly mentioned a time range
        DATE_KEYWORDS = [
            "hôm nay", "today", "hôm qua", "yesterday", "ngày mai", "tomorrow",
            "tuần này", "this week", "tuần trước", "last week",
            "tháng này", "this month", "tháng trước", "last month", "tháng sau",
            "tháng 1", "tháng 2", "tháng 3", "tháng 4", "tháng 5", "tháng 6",
            "tháng 7", "tháng 8", "tháng 9", "tháng 10", "tháng 11", "tháng 12",
            "january", "february", "march", "april", "may", "june",
            "july", "august", "september", "october", "november", "december",
            "quý này", "this quarter", "quý trước", "last quarter",
            "năm nay", "this year", "năm ngoái", "last year",
            "ngày", "month", "week", "year", "quarter",
            "202",  # catches any explicit date like 2026-xx-xx
        ]
        user_mentioned_date = any(kw in lower_msg for kw in DATE_KEYWORDS)

        # Keywords for granularity
        GRANULARITY_KEYWORDS = [
            "daily", "weekly", "monthly", "theo ngày", "theo tuần", "theo tháng",
            "hàng ngày", "hàng tuần", "hàng tháng", "chia theo",
        ]
        user_mentioned_granularity = any(kw in lower_msg for kw in GRANULARITY_KEYWORDS)

        # salesOrg codes
        SALES_ORG_CODES = ["ue00", "uw00", "dn00", "ds00"]
        user_mentioned_sales_org = any(code in lower_msg for code in SALES_ORG_CODES)

        # top N – user must mention a number with limit intent
        import re as _re
        _top_val = str(args.get("top", ""))
        user_mentioned_top = bool(
            _re.search(r"top\s*\d+", lower_msg) or
            _re.search(r"\d+\s*(cái|kết quả|records?|items?)", lower_msg) or
            (_top_val.isdigit() and _top_val in lower_msg) or
                    "nhất" in lower_msg
        )

        # Detect open-ended "from/since/kể từ [date]" pattern (no explicit end date)
        OPEN_ENDED_KEYWORDS = [
            "kể từ", "since ", "from ", "starting from",
            "bắt đầu từ", "created since",
            "từ 20",   # catches 'from 2026-xx-xx' in Vietnamese
            "tính từ",  # another VN 'since' expression
        ]
        END_DATE_KEYWORDS = ["đến", " to ", "until", "through", "before", "ending", "tới", "đến ngày"]
        is_open_ended = (
            any(kw in lower_msg for kw in OPEN_ENDED_KEYWORDS) and
            not any(kw in lower_msg for kw in END_DATE_KEYWORDS)
        )

        for k, v in args.items():
            # Skip None / null / empty
            if v is None or str(v).strip() == "" or str(v).lower() == "null":
                continue
            # Strip hallucinated date params
            if k in ("fromDate", "toDate") and not user_mentioned_date:
                continue
            # Strip hallucinated toDate for open-ended "since/kể từ [date]" queries
            if k == "toDate" and is_open_ended:
                continue
            # Strip hallucinated granularity
            if k == "granularity" and not user_mentioned_granularity:
                continue
            # Strip hallucinated salesOrg
            if k == "salesOrg" and not user_mentioned_sales_org:
                continue
            # Strip hallucinated top
            if k == "top" and not user_mentioned_top:
                continue
            cleaned[k] = v
        args = cleaned

    return fn_name, args


def parse_and_validate_failed_generation(
    exc: openai.BadRequestError, query: str
) -> tuple[str | None, dict] | None:
    """
    Extracts function name and arguments from failed_generation inside a BadRequestError,
    performs parameter validation, and returns (pred_fn, pred_args) or None if it cannot parse/recover.
    """
    try:
        failed_gen = None
        body = getattr(exc, "body", None)
        if isinstance(body, dict):
            failed_gen = body.get("error", {}).get("failed_generation")

        if not failed_gen:
            exc_str = str(exc)
            func_idx = exc_str.find("<function=")
            if func_idx != -1:
                match_func = re.search(
                    r"(<function=.*?>.*?<function>|<function=.*?>.*?</function>|<function=.*?>.*?(?=['\"]|\}))",
                    exc_str[func_idx:],
                    re.DOTALL,
                )
                if match_func:
                    failed_gen = match_func.group(1)

        if not failed_gen:
            return None

        match = re.search(
            r"<function=(\w+)>(.*?)(?:<function>|</function>|$)", failed_gen, re.DOTALL
        )
        if not match:
            return None

        fn_name = match.group(1)
        raw_args = match.group(2).strip()

        try:
            args = json.loads(raw_args) if raw_args else {}
        except json.JSONDecodeError:
            try:
                args = json.loads(raw_args.replace("'", '"'))
            except Exception:
                args = {}

        validated = validate_parameters(fn_name, args, query)
        if validated is None:
            return None, {}
        return validated
    except Exception:
        return None


# ---------------------------------------------------------------------------
# Main Evaluation Loop
# ---------------------------------------------------------------------------


# ---------------------------------------------------------------------------
# Fuzzy Argument Matching
# ---------------------------------------------------------------------------

# Required params per function — MUST match exactly in both intent and value.
# Optional params (not listed) are "bonus": AI may or may not include them.
_REQUIRED_PARAMS: dict[str, set[str]] = {
    "CheckOrderStatus":  {"order_id"},
    "GetOrderDetail":    {"order_id"},
    "ReleaseOrder":      {"order_id"},
    "RejectOrder":       {"order_id", "reason_code"},
    "ForwardOrder":      {"order_id", "forward_to_user"},
    # KPI / list functions have NO required params
    "GetSalesOrders":    set(),
    "GetKpiSummary":     set(),
    "GetKpiByCustomer":  set(),
    "GetKpiByProduct":   set(),
    "GetOverdueOrders":  set(),
}


def _normalize_val(v) -> str:
    """Lowercase, strip trailing punctuation for lenient string comparison."""
    s = str(v).strip()
    return s.rstrip(".").strip().lower()


def fuzzy_args_match(pred_fn: str | None, pred_args: dict,
                     exp_fn: str | None, exp_args: dict) -> tuple[bool, bool]:
    """
    Returns (intent_ok, args_ok).

    Fuzzy rules:
    - Required params: must all be present AND values must match (normalized).
    - Expected optional params: must be present AND match if AI also returned them.
      If AI omitted an optional expected param → still OK (model chose not to include).
    - Extra params from AI (not in expected): ignored — not counted as failure.
    - Trap queries (exp_fn=None): AI must also return None.
    """
    intent_ok = pred_fn == exp_fn
    if not intent_ok:
        return False, False
    if exp_fn is None:
        return True, True  # both None → full pass

    required = _REQUIRED_PARAMS.get(exp_fn, set())

    def norm(d: dict) -> dict[str, str]:
        return {k: _normalize_val(v) for k, v in d.items()
                if v is not None and str(v).lower() not in ("null", "")}

    p = norm(pred_args)
    e = norm(exp_args)

    # 1. Required params: must match
    for key in required:
        if e.get(key) != p.get(key):  # missing or wrong value
            return True, False

    # 2. Optional expected params: if AI returned them, values must match
    for key, exp_val in e.items():
        if key in required:
            continue  # already checked
        if key in p and p[key] != exp_val:
            return True, False  # AI returned it but with wrong value
        # AI omitted it → acceptable (lenient)

    return True, True


def run_evaluation(live_mode: bool = False, skip_confirm: bool = False,
                   tier_filter: int | None = None, fuzzy: bool = True,
                   from_line: int | None = None, to_line: int | None = None):
    # Load golden dataset
    if not GOLDEN_DATA_PATH.exists():
        print(f"Error: Golden dataset not found at {GOLDEN_DATA_PATH}", file=sys.stderr)
        sys.exit(1)

    test_cases = []
    with open(GOLDEN_DATA_PATH, "r", encoding="utf-8") as f:
        for idx, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                test_cases.append(json.loads(line))
            except Exception as e:
                print(
                    f"Error parsing line {idx} in {GOLDEN_DATA_PATH.name}: {e}",
                    file=sys.stderr,
                )
                sys.exit(1)

    total_loaded = len(test_cases)

    # Filter by line range (1-indexed, takes priority over tier if both specified)
    if from_line is not None or to_line is not None:
        lo = (from_line or 1) - 1      # convert to 0-indexed
        hi = (to_line or total_loaded)  # inclusive
        test_cases = test_cases[lo:hi]
        print(f"Loaded {len(test_cases)}/{total_loaded} test cases "
              f"[lines {from_line or 1}–{to_line or total_loaded}] from {GOLDEN_DATA_PATH.name}")
    elif tier_filter is not None:
        test_cases = [c for c in test_cases if c.get("tier", 3) <= tier_filter]
        tier_label = {1: "Smoke (Tier 1)", 2: "Core (Tier ≤ 2)", 3: "Full (Tier ≤ 3)"}[tier_filter]
        print(f"Loaded {len(test_cases)}/{total_loaded} test cases [{tier_label}] from {GOLDEN_DATA_PATH.name}")
    else:
        print(f"Loaded {total_loaded} test cases from {GOLDEN_DATA_PATH.name}")

    CACHE_PATH = SCRIPT_DIR / "test_cache.json"
    cache = {}
    if CACHE_PATH.exists():
        try:
            cache = json.loads(CACHE_PATH.read_text(encoding="utf-8"))
        except Exception as e:
            print(f"Warning: Failed to load cache file: {e}", file=sys.stderr)
            cache = {}

    # Quota Safety Warning Checks
    needed_calls = (
        len(test_cases)
        if live_mode
        else sum(1 for case in test_cases if case["query"] not in cache)
    )

    if needed_calls > 5 and not skip_confirm:
        print(f"\n[CẢNH BÁO] Hệ thống sẽ thực hiện gọi API thực tế {needed_calls} lần.")
        print(
            "Số lượng cuộc gọi này có thể làm cạn quota hoặc vượt quá giới hạn Rate Limit của tài khoản."
        )
        try:
            confirm = (
                input("Bạn có chắc chắn muốn tiếp tục không? (y/N): ").strip().lower()
            )
            if confirm not in ("y", "yes"):
                print("Đã hủy bởi người dùng.")
                sys.exit(0)
        except (KeyboardInterrupt, EOFError):
            print("\nĐã hủy.")
            sys.exit(0)

    cache_hits = 0
    api_calls = 0

    client = None
    system_prompt = ""
    tools = []
    model_name = ""

    if not GROQ_API_KEY or "your_" in GROQ_API_KEY:
        print(
            "Error: GROQ_API_KEY is missing/placeholder. API key is required to run the evaluation.",
            file=sys.stderr,
        )
        sys.exit(1)

    print("Initializing Groq Client...")
    try:
        client = OpenAI(api_key=GROQ_API_KEY, base_url="https://api.groq.com/openai/v1")
        system_prompt = _load_system_prompt()
        tools = load_tools()
        model_name = os.getenv("GROQ_MODEL", "llama-3.1-8b-instant")
        print(f"Running evaluation using model: {model_name}\n")
    except Exception as e:
        print(f"Failed to initialize API client: {e}", file=sys.stderr)
        sys.exit(1)

    results = []
    correct_intents = 0
    correct_args = 0

    # Format printing helpers
    row_format = "{:<3} | {:<40} | {:<20} | {:<20} | {:<6} | {:<6}"
    print("-" * 110)
    print(
        row_format.format(
            "No", "Query", "Expected Function", "Predicted Function", "Intent", "Args"
        )
    )
    print("-" * 110)

    for idx, case in enumerate(test_cases, 1):
        query = case["query"]
        expected_fn = case["expected_function"]
        expected_args = case.get("expected_args") or {}

        pred_fn = None
        pred_args = {}
        # Check cache first (only if not live_mode)
        if not live_mode and query in cache:
            pred_fn = cache[query].get("predicted_function")
            pred_args = cache[query].get("predicted_args") or {}
            cache_hits += 1
        else:
            # Invoke Groq API
            api_calls += 1
            try:
                # Random backoff sleep between 0.5s and 2.0s
                import random

                sleep_time = random.uniform(0.5, 2.0)
                time.sleep(sleep_time)

                messages = [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": query},
                ]
                try:
                    response = generate_content_with_retry(
                        client=client, model=model_name, messages=messages, tools=tools
                    )
                    choice = response.choices[0]
                    tool_calls = getattr(choice.message, "tool_calls", None) or []
                    if tool_calls and tool_calls[0].type == "function":
                        pred_fn = tool_calls[0].function.name
                        try:
                            pred_args = (
                                json.loads(tool_calls[0].function.arguments)
                                if tool_calls[0].function.arguments
                                else {}
                            )
                        except json.JSONDecodeError:
                            pred_args = {}

                        # Validate parameters
                        validated = validate_parameters(pred_fn, pred_args, query)
                        if validated is None:
                            pred_fn = None
                            pred_args = {}
                        else:
                            pred_fn, pred_args = validated
                    else:
                        pred_fn = None
                        pred_args = {}
                except openai.BadRequestError as bad_req_exc:
                    recovered = parse_and_validate_failed_generation(bad_req_exc, query)
                    if recovered is not None:
                        pred_fn, pred_args = recovered
                    else:
                        raise bad_req_exc

                # Save successful result to cache
                cache[query] = {
                    "predicted_function": pred_fn,
                    "predicted_args": pred_args,
                }
                try:
                    CACHE_PATH.write_text(
                        json.dumps(cache, indent=2, ensure_ascii=False),
                        encoding="utf-8",
                    )
                except Exception as e:
                    print(f"Warning: Failed to save cache: {e}", file=sys.stderr)
            except Exception as exc:
                print(f"\n[ERR] Query {idx} failed (API error): {exc}", file=sys.stderr)
                import traceback

                traceback.print_exc(file=sys.stderr)
                pred_fn = "API_FAIL"
                pred_args = {}

        if fuzzy:
            intent_match, args_match = fuzzy_args_match(
                pred_fn, pred_args, expected_fn, expected_args
            )
        else:
            # Legacy exact match
            intent_match = pred_fn == expected_fn
            normalized_pred_args = {
                k: str(v) for k, v in pred_args.items()
                if v is not None and str(v).lower() != "none"
            }
            normalized_expected_args = {
                k: str(v) for k, v in expected_args.items()
                if v is not None and str(v).lower() != "none"
            }
            args_match = normalized_pred_args == normalized_expected_args if intent_match else False

        if intent_match:
            correct_intents += 1
        if args_match:
            correct_args += 1

        results.append(
            {
                "query": query,
                "expected_fn": expected_fn,
                "expected_args": expected_args,
                "pred_fn": pred_fn,
                "pred_args": pred_args,
                "intent_match": intent_match,
                "args_match": args_match,
            }
        )

        # Truncate query for terminal display if too long
        disp_query = query[:37] + "..." if len(query) > 40 else query
        disp_exp_fn = str(expected_fn)
        disp_pred_fn = str(pred_fn)
        print(
            row_format.format(
                idx,
                disp_query,
                disp_exp_fn,
                disp_pred_fn,
                "PASS" if intent_match else "FAIL",
                "PASS" if args_match else "FAIL",
            )
        )

    print("-" * 110)

    # Detailed mismatch summary
    mismatches = [r for r in results if not r["intent_match"] or not r["args_match"]]
    if mismatches:
        print("\n--- Detailed Mismatches ---")
        for idx, m in enumerate(mismatches, 1):
            print(f"\nMismatch #{idx}:")
            print(f"  Query:      {m['query']}")
            print(
                f"  Expected:   Function: {m['expected_fn']}, Args: {m['expected_args']}"
            )
            print(f"  Predicted:  Function: {m['pred_fn']}, Args: {m['pred_args']}")
            print(f"  Mismatch:   Intent: {m['intent_match']}, Args: {m['args_match']}")
        print("-" * 110)

    # Compute final statistics
    total = len(test_cases)

    error_count = sum(1 for r in results if r["pred_fn"] in ("API_FAIL", "ERROR"))
    pass_count = sum(1 for r in results if r["intent_match"] and r["args_match"])
    fail_count = total - pass_count - error_count

    intent_accuracy = (correct_intents / total) * 100
    args_accuracy = (correct_args / total) * 100

    print("\n--- Accuracy Statistics ---")
    print(f"Tổng số test cases:                                {total}")
    print(f"Số câu đúng hoàn toàn (Intent & Args khớp - PASS):  {pass_count} / {total}")
    print(f"Số câu sai lệch logic (AI logic mismatch - FAIL):   {fail_count} / {total}")
    print(
        f"Số câu lỗi API/Mạng (API/Network error - ERROR):    {error_count} / {total}"
    )
    print(f"Tỷ lệ chính xác về chọn hàm (KPI: >= 85%):          {intent_accuracy:.2f}%")
    print(f"Tỷ lệ chính xác về bóc tách tham số (KPI: >= 80%):  {args_accuracy:.2f}%")
    print(f"Cache Hits (Số lượng lấy từ cache):                {cache_hits}")
    print(f"Actual API Calls (Số lượng gọi API):               {api_calls}")
    print("-" * 110)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Evaluate NLU Intent and Parameter Accuracy."
    )
    parser.add_argument(
        "--live",
        action="store_true",
        help="Bypass local cache and perform live API calls for all cases.",
    )
    parser.add_argument("--no-cache", action="store_true", help="Alias for --live.")
    parser.add_argument(
        "-y",
        "--yes",
        action="store_true",
        help="Skip confirmation warning for live API calls.",
    )
    parser.add_argument(
        "--smoke",
        action="store_true",
        help="Run only Tier-1 smoke tests (~13 cases, 1 per function). Fast & cheap.",
    )
    parser.add_argument(
        "--tier",
        type=int,
        choices=[1, 2, 3],
        default=None,
        help="Run only cases of a specific tier (1=smoke, 2=core, 3=full).",
    )
    parser.add_argument(
        "--from",
        dest="from_line",
        type=int,
        default=None,
        metavar="N",
        help="Start from line N (1-indexed). Example: --from 14",
    )
    parser.add_argument(
        "--to",
        dest="to_line",
        type=int,
        default=None,
        metavar="N",
        help="Stop at line N inclusive. Example: --to 65",
    )
    parser.add_argument(
        "--fuzzy",
        action="store_true",
        default=True,
        help="(default ON) Allow AI to return superset of expected optional params.",
    )
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Disable fuzzy matching — exact arg comparison (legacy behaviour).",
    )
    args = parser.parse_args()

    tier_filter = 1 if args.smoke else args.tier  # --smoke ≡ --tier 1
    fuzzy = not args.strict
    run_evaluation(
        live_mode=(args.live or args.no_cache),
        skip_confirm=args.yes,
        tier_filter=tier_filter,
        fuzzy=fuzzy,
        from_line=args.from_line,
        to_line=args.to_line,
    )
