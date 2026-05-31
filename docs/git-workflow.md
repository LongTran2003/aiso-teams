# Git Workflow Rules — AISO-Teams

> Document này là bộ rule **bắt buộc** cho mọi thành viên team về cách sử dụng Git, đẩy code, tạo PR, review code. Mục đích: đảm bảo code chất lượng, history clean, audit trail rõ ràng cho thesis defense, và team không phá nhau.

## 1. Branch model

### 1.1. Protected branches (không push trực tiếp)

| Branch | Mục đích | Protection |
|---|---|---|
| `main` | Production-ready code, deploy từ đây | PR required, 1 approval, CI pass |
| `develop` | Integration branch (default branch) | PR required, CI pass |

### 1.2. Working branches

| Type | Format | Khi nào dùng |
|---|---|---|
| `feature/` | `feature/{role}-{description}` | Feature mới |
| `fix/` | `fix/{description}` | Bug fix |
| `chore/` | `chore/{description}` | CI, refactor, infrastructure, non-functional |
| `docs/` | `docs/{description}` | Documentation only |
| `hotfix/` | `hotfix/{description}` | Urgent production fix (rare) |

**Role identifiers:**
- `be` — Backend (.NET)
- `ai` — AI module
- `sap` — SAP code (ABAP/CDS/RAP)
- `fe` — Frontend (Teams app + Adaptive Cards)
- `infra` — Infrastructure / Docker / DevOps
- `integration` — Cross-role features

### 1.3. Branch lifecycle

```
1. Create from develop:    git checkout -b feature/be-sap-client
2. Work + commit + push:   git push -u origin feature/be-sap-client
3. Create PR to develop
4. Review + CI check
5. Merge to develop
6. Delete branch (local + remote)
```

### 1.4. Branch naming examples

✅ **Good:**
- `feature/be-sap-client`
- `feature/ai-function-registry`
- `feature/sap-cds-revenue-view`
- `fix/be-token-refresh-bug`
- `chore/update-postgres-v17`
- `docs/sap-setup-guide`

❌ **Bad:**
- `Long-test` (too vague, no role)
- `feature1` (no description)
- `feature/My New Branch` (spaces, capitals)
- `fix-the-thing` (no role, no slash separator)

## 2. Commit message convention

### 2.1. Format

```
<type>(<scope>): <description>

[optional body explaining WHY]

[optional footer with issue references]
```

### 2.2. Types

| Type | Khi nào dùng |
|---|---|
| `feat` | New feature |
| `fix` | Bug fix |
| `refactor` | Code restructure, không change behavior |
| `docs` | Documentation updates |
| `chore` | Build, CI, dependencies, non-functional |
| `test` | Adding or updating tests |
| `perf` | Performance improvement |
| `style` | Formatting, whitespace only |

### 2.3. Scopes (giống role identifiers)

`be`, `ai`, `sap`, `fe`, `infra`, `ci`, `docs`

### 2.4. Rules

- First line **≤ 72 ký tự**
- Imperative mood: "add" not "added" / "adds"
- **Lowercase** after colon
- **No period** at end of subject line
- Body (optional): explain **WHY** not WHAT, separated by blank line

### 2.5. Examples

✅ **Good:**
```
feat(be): add SapClient with mock implementation

Mock returns sample SO data từ JSON file. Cho phép AI/FE
development không bị block đợi SAP team.

Refs: #15
```

```
fix(ai): correct token counting for Vietnamese text
```

```
chore(infra): update PostgreSQL to v16.2
```

```
docs(api): document SO query endpoint
```

❌ **Bad:**
```
Updated stuff                          # No type, no scope, vague
fix bug                                # What bug? Where?
WIP                                    # Don't commit WIP to shared branches
Fixed the thing that was broken.       # Period at end, vague
FIXED BUG IN BACKEND                   # All caps
```

## 3. Daily workflow

### 3.1. Bắt đầu work mới

```bash
# Pull latest develop
git checkout develop
git pull

# Create feature branch
git checkout -b feature/be-sap-client

# Work + commit thường xuyên
git add .
git commit -m "feat(be): add ISapClient interface"

# Push frequently (avoid losing work)
git push -u origin feature/be-sap-client
```

**Rule:** Push branch lên remote **ít nhất 1 lần/ngày** ngay cả khi feature chưa xong → backup khỏi mất work nếu máy hỏng.

### 3.2. Trong khi coding

- Commit **small, focused changes**
- 1 logical change per commit
- Verify build pass before commit: `dotnet build`
- Run tests if available: `dotnet test`

### 3.3. Hoàn thành feature

1. **Self-review** code locally — đọc lại `git diff` xem có code thừa, comment debug, secrets không
2. **Verify build** + test pass
3. **Push final commits**
4. **Create PR** với proper template
5. **Self-assign + add reviewers + labels** (xem section 4)
6. **Address review feedback** — push thêm commits, không force push
7. **After approval:** merge
8. **Cleanup:** delete branch local + remote

## 4. Tạo PR đúng cách

### 4.1. Step-by-step

#### Step 1: Push branch lên remote (nếu chưa)

```bash
git push -u origin feature/be-sap-client
```

#### Step 2: Tạo PR trên GitHub

- Vào repo trên GitHub → banner xanh **"Compare & pull request"** → click
- Hoặc: tab **Pull requests** → **New pull request** → chọn branch → **Create pull request**

#### Step 3: Fill PR template

Template auto-load (đã setup từ `.github/pull_request_template.md`):

- **Title**: ngắn gọn, follow commit convention
  - Format: `<type>(<scope>): <description>`
  - Example: `feat(be): add SapClient with mock implementation`

- **Description**: 
  - Tóm tắt thay đổi 1-2 đoạn
  - Bullet points nếu nhiều thay đổi
  - Link tới design doc / API contract nếu có
  - Screenshot UI nếu là FE
  - Test plan: cách bạn đã test

- **Loại thay đổi**: tick checkbox
- **Role / Module**: tick checkbox
- **Checklist**: tick từng item đã làm
- **Related issues**: link issue nếu có

#### Step 4: Add Assignees

⚠️ **Bắt buộc self-assign chính mình** vào field **Assignees** (panel bên phải)

→ Để biết PR nào của ai trong board view.

#### Step 5: Reviewers (auto + manual)

**Auto-assigned by CODEOWNERS:**
- Backend changes → BE lead + Leader
- SAP code → SAP team
- AI code → AI member
- Infrastructure → Leader

**Add manual reviewers nếu cần:**
- Cross-role review (vd: BE PR ảnh hưởng FE → add FE reviewer)
- Domain expert review (vd: SAP query → add SAP team)

#### Step 6: Labels

Add labels tương ứng (panel bên phải, **Labels**):

| Label | Khi nào dùng |
|---|---|
| `feature` | PR feature mới |
| `bug` | Bug fix |
| `chore` | Maintenance, CI |
| `documentation` | Docs only |
| `be` / `ai` / `sap` / `fe` / `infra` | Role label |
| `breaking-change` | Phá compatibility |
| `priority: high` | Cần merge gấp |
| `WIP` | Work in progress, đừng merge |

Tạo labels trong **Settings → Labels** trước khi dùng (Leader làm 1 lần).

#### Step 7: Project / Milestone (optional)

Nếu dùng GitHub Projects board cho sprint tracking → add PR vào project.

#### Step 8: Click "Create pull request"

🎉 PR đã tạo. CI sẽ chạy automatically. Reviewers nhận notification.

### 4.2. PR title vs commit message

| | Format | Examples |
|---|---|---|
| **Commit messages** | Có thể có nhiều, mỗi commit 1 type | `feat(be): add interface`, `feat(be): add mock impl` |
| **PR title** | 1 dòng tổng kết toàn bộ work | `feat(be): add SapClient with mock implementation` |

Khi merge PR, GitHub có thể squash commits → PR title trở thành 1 commit duy nhất trong develop.

### 4.3. PR size rules

| PR size | Lines changed | Khi nào dùng |
|---|---|---|
| **XS** | < 50 lines | Bug fixes, tiny changes |
| **S** | 50-200 lines | Small features |
| **M** | 200-500 lines | Medium features (recommend max) |
| **L** | 500-1000 lines | Large features (split nếu được) |
| **XL** | > 1000 lines | ❌ Avoid — split mandatorily |

**Tại sao PR nhỏ?**
- Review nhanh hơn
- Bug ít hơn
- Conflict ít hơn
- Merge dễ hơn

## 5. Code review responsibilities

### 5.1. Author (người tạo PR)

- Self-review trước khi request review
- Respond comments **trong 24h working day**
- Không force push sau khi reviewer đã review (use new commits thay vì amend)
- Đánh dấu **"Resolved"** mỗi comment sau khi fix
- Re-request review sau khi push fix

### 5.2. Reviewer

- Review **trong 24h working day** (sooner is better)
- Constructive feedback — explain WHY, suggest solution
- Use GitHub **Suggestion** feature cho small changes
- Approve only when **truly confident**
- Block (request changes) nếu có serious issue
- "LGTM" alone không đủ — phải có actual review trước

### 5.3. Leader

- Final approval cho high-impact changes
- Resolve cross-team conflicts
- Merge decision cho PRs cuối sprint

### 5.4. Review checklist

- ☐ Code build pass (CI green)
- ☐ Tests pass
- ☐ Code quality OK (no obvious code smells)
- ☐ Security: no secrets, SQL injection, XSS, etc.
- ☐ Documentation updated nếu API changed
- ☐ Performance acceptable
- ☐ Error handling adequate
- ☐ Follows team conventions

## 6. Merging rules

### 6.1. Merge requirements

PR có thể merge khi đủ TẤT CẢ:
- ✅ CI green (build + tests pass)
- ✅ Required reviewers approved (per CODEOWNERS)
- ✅ Conversations resolved
- ✅ Branch up to date với develop (no conflicts)

### 6.2. Merge strategies

GitHub support 3 strategies. Team mình dùng:

| Strategy | Khi nào dùng | Preserved commits |
|---|---|---|
| **Squash and merge** | Default cho feature branches | 1 commit duy nhất |
| **Merge commit** | Cho release PR (develop → main) | All commits + merge commit |
| **Rebase and merge** | Tránh dùng (gây confused history) | All commits, linear |

### 6.3. Ai có quyền merge?

| PR target | Who can merge |
|---|---|
| `develop` (feature PRs) | Author tự merge sau approval (per CODEOWNERS) |
| `main` (release PRs) | **Chỉ Leader** merge |
| Hotfix to `main` | Leader + 1 senior member approve |

### 6.4. Sau khi merge

```bash
# Pull merged changes về local
git checkout develop
git pull

# Delete local feature branch
git branch -d feature/be-sap-client

# Delete remote branch (GitHub có thể auto-delete)
git push origin --delete feature/be-sap-client
```

## 7. Tuyệt đối KHÔNG làm

| ❌ Không làm | ✅ Thay vào đó |
|---|---|
| Push trực tiếp vào `main` hoặc `develop` | Tạo feature branch + PR |
| Force push (`--force`) vào shared branch | `--force-with-lease` chỉ cho own feature branch |
| Merge own PR khi chưa được approve (non-trivial) | Đợi reviewer |
| Commit secrets (`.env`, API keys, passwords) | Add vào `.gitignore`, dùng env variables |
| Commit binary files (`.zip`, `.exe`, `.dll`) | Git LFS hoặc artifact storage |
| Commit broken code | Verify `dotnet build` pass trước commit |
| Huge PRs (>500 lines) | Split thành nhiều PRs nhỏ |
| Mix unrelated changes trong 1 PR | 1 PR = 1 logical change |
| Skip writing PR description | Always describe what + why |
| Ignore review comments | Address hoặc explain why won't fix |
| Merge khi CI red | Fix CI trước khi merge |
| Rebase / amend pushed commits | Add new commits |

## 8. Merge conflicts

### 8.1. Prevention

- Pull develop **mỗi ngày** vào feature branch
- Communicate sớm khi work overlap với người khác
- Keep PRs small + short-lived (max 3-5 ngày)

### 8.2. Resolution

```bash
# 1. Đảm bảo có latest develop
git checkout develop
git pull

# 2. Quay lại feature branch
git checkout feature/my-feature

# 3. Merge develop vào feature branch
git merge develop

# 4. Conflicts xuất hiện trong files - mở từng file, resolve manually
#    Tìm các markers: <<<<<<<, =======, >>>>>>>
#    Quyết định keep cái nào, xóa markers

# 5. Stage resolved files
git add <resolved-files>

# 6. Complete merge
git commit
# (Hoặc git rebase --continue nếu rebase)

# 7. Push
git push
```

### 8.3. Khi nào gọi help

Liên hệ Leader nếu:
- Conflict quá phức tạp (>20 files)
- Không hiểu logic của code conflict
- Sợ làm mất changes của ai khác

**Đừng panic, đừng force push.** Worst case, branch luôn có backup trên remote.

## 9. Common workflows

### 9.1. Sync feature branch với develop mới nhất

```bash
git checkout develop
git pull
git checkout feature/my-feature
git merge develop
git push
```

### 9.2. Undo last commit (chưa push)

```bash
# Giữ changes, chỉ undo commit
git reset --soft HEAD~1

# Discard changes hoàn toàn
git reset --hard HEAD~1   # ⚠️ destructive
```

### 9.3. Forgot to add file vào commit cuối

```bash
git add forgotten-file
git commit --amend --no-edit
# Nếu đã push: git push --force-with-lease (chỉ trên own feature branch)
```

### 9.4. Edit commit message gần nhất

```bash
git commit --amend
# Editor mở ra, sửa message, save, exit
```

### 9.5. Stash uncommitted changes tạm thời

```bash
git stash
# Switch branch, làm việc khác
git stash pop  # Phục hồi changes
```

### 9.6. Xem ai change dòng code này

```bash
git blame <filename>
git blame -L 10,20 <filename>  # chỉ dòng 10-20
```

## 10. Emergency procedures

### 10.1. Hotfix to main

```bash
# Create from main, not develop
git checkout main
git pull
git checkout -b hotfix/critical-auth-bug

# Make fix, commit
git commit -m "fix(be): critical token validation bug"
git push -u origin hotfix/critical-auth-bug

# PR to main (Leader approval required)
# After merge to main, also merge to develop:
git checkout develop
git pull
git merge main
git push
```

### 10.2. Revert bad merge

```bash
# Find the merge commit SHA
git log --oneline -10

# Revert it (create new commit that undoes the merge)
git revert -m 1 <merge-commit-sha>

# Push as PR
```

**Không force push để undo** — sẽ phá history của người khác.

### 10.3. Recover deleted branch / lost commits

```bash
# Git reflog ghi mọi HEAD changes trong 30 ngày
git reflog

# Find the SHA of lost commit, then:
git checkout -b recovery <commit-sha>
```

Branch reborn, work safe.

### 10.4. Khi rối loạn — ask Leader

Đừng tự fix nếu không chắc. Better:
- Stop, commit work-in-progress
- Push branch lên remote
- Ping Leader screenshot trạng thái
- Leader debug help

## 11. Communication around PRs

### 11.1. Slack / Teams message khi:

- PR cần urgent review (blocking other work)
- PR có high-impact (breaking changes)
- PR bị stuck > 48h không review
- Conflict cross-team cần discuss

### 11.2. Format ping message

```
@reviewer-name PR #42 cần review giúp em
Title: feat(be): add SapClient with mock
Impact: AI member cần để continue work
Link: <github-url>
```

### 11.3. PR comments

- Constructive feedback only
- "Why this approach" instead of "this is wrong"
- Use suggestion feature cho 1-line fixes
- Tag specific people: `@username`

## 12. Quick reference

### 12.1. Useful Git commands

```bash
# Status & history
git status
git log --oneline -20
git log --graph --all --oneline -20
git diff
git diff develop                    # diff với develop
git log develop..HEAD               # commits trong feature branch

# Branch operations
git checkout -b new-branch
git branch -d old-branch            # delete local
git push origin --delete old-branch # delete remote
git fetch --all                     # sync remote info

# Remote
git push -u origin <branch>         # first push (set upstream)
git push                            # subsequent pushes
git pull
git fetch origin

# Undo
git reset --soft HEAD~1
git reset --hard HEAD~1             # ⚠️ destructive
git revert <sha>
git checkout -- <file>              # discard changes in file

# Stash
git stash
git stash list
git stash pop
git stash drop

# Investigation
git blame <file>
git show <sha>
git reflog
```

### 12.2. Aliases gợi ý (optional, set 1 lần)

```bash
git config --global alias.st status
git config --global alias.co checkout
git config --global alias.br branch
git config --global alias.ci commit
git config --global alias.lg "log --oneline --graph --all -20"
```

Sau đó dùng: `git st`, `git co`, `git lg`, etc.

## 13. PR Review SLAs

| Priority | First review SLA | Resolution SLA |
|---|---|---|
| `priority: high` | 4 hours | 1 day |
| Normal | 24 hours | 3 days |
| `priority: low` | 48 hours | 1 week |

Nếu reviewer không respond trong SLA → escalate Leader.

---

## Appendix: Checklist trước khi tạo PR

Self-review trước khi push:

- ☐ Code build pass (`dotnet build`)
- ☐ Tests pass (`dotnet test`) nếu có
- ☐ No commented-out code
- ☐ No `Console.WriteLine` / `Debug.WriteLine` debug
- ☐ No hardcoded secrets / passwords / API keys
- ☐ `.gitignore` properly exclude generated files
- ☐ Branch name follow convention
- ☐ Commit messages follow convention
- ☐ Files only related to this feature
- ☐ Documentation updated nếu API change
- ☐ Đã pull develop mới nhất (no conflicts)

Sau khi tạo PR:

- ☐ Title clear, follow convention
- ☐ Description đầy đủ (what + why)
- ☐ Self-assigned (Assignees)
- ☐ Reviewers added (auto + manual nếu cần)
- ☐ Labels added
- ☐ Checklist trong PR body đã tick
- ☐ Linked issue (nếu có)

---

> **Câu hỏi / suggest update document:** mention @leader trong Teams channel "AISO-Project".
> **Version:** 1.0 — last updated: 2026-05-31
