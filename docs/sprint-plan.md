# Sprint Plan — AISO-Teams

**Project**: AISO-Teams (AI-Powered Microsoft Teams Chatbot for SAP Sales Order Management)
**Duration**: May 2026 – August 2026 (~14 weeks)
**Methodology**: Phased delivery with Scrum-inspired 2-week sprints
**Team**: 5 members (1 BE Lead + 2 SAP + 2 AI/FE)
**Mentor**: FSoft Mentor (TBD) + School Supervisor: Thầy Nguyễn Ba Lê

---

## Table of Contents

- [Project Phases Overview](#project-phases-overview)
- [Mentor Review Milestones](#mentor-review-milestones)
- [Team Roles and Responsibilities](#team-roles-and-responsibilities)
- [Methodology](#methodology)
- [Sprint Ceremonies](#sprint-ceremonies)
- [Definition of Done](#definition-of-done)
- [Quality Metrics](#quality-metrics)
- [Sprint Schedule](#sprint-schedule)
- [Sprint Details](#sprint-details)
- [Velocity Tracking](#velocity-tracking)
- [Risk Management](#risk-management)
- [Scope Management](#scope-management)
- [Appendix: Templates](#appendix-templates)

---

## Project Phases Overview

The project follows a **4-phase delivery model** aligned with FSoft mentor reviews and the academic defense calendar.

```
Phase 1                  Phase 2                                Phase 3       Phase 4
Preparation +            Realization                            UAT           Defense
Explore                  (Development)                                        (Golive)
─────────                ──────────────────────────────────     ───────       ─────────
16/05 — 17/05            19/05 — 27/07                          30/07 — 05/08  23/08
2 days                   10 weeks (5 sprints)                   1 week         2.5 weeks
        │                              │                               │             │
        ▼                              ▼                               ▼             ▼
   Mentor Review 1            Mentor Review 2                Mentor Review 3   Thesis Defense
```

### Phase 1: Preparation + Explore (16/05 – 17/05)

**Duration**: 2 days

**Goals**:
- Project kick-off and team alignment
- Research SAP functions (SD module, Sales Order processes)
- Prepare workshop slides for mentor introduction
- Confirm team distribution and roles

**Deliverables**:
- Kick-off meeting completed with team and supervisor
- Initial SAP function research document
- Workshop slides for mentor presentation

**Ends with**: 🎯 **Mentor Review 1** (17/05)

---

### Phase 2: Realization (19/05 – 27/07)

**Duration**: 10 weeks → **5 sprints × 2 weeks**

**Goals**:
- Build complete working chatbot end-to-end
- Implement all main requirements (SO queries, KPIs, workflows, reports)
- Implement AI Bonus features (insights generation)
- Write Technical Specification (incrementally per sprint)
- Complete Testing
- Write User Manual

**Deliverables**:
- Working bot in Microsoft Teams
- All 15+ functions implemented
- Technical Specification document (incremental, finalized in Sprint 5)
- Testing documentation
- User Manual

**Ends with**: 🎯 **Mentor Review 2** (27/07) — full working software demo

---

### Phase 3: UAT (30/07 – 05/08)

**Duration**: 1 week (Sprint 6 — short sprint)

**Goals**:
- User Acceptance Testing with mentor and stakeholders
- Bug fixes from UAT findings
- UAT sign-off documentation

**Deliverables**:
- UAT test scenarios executed
- Bug fixes for issues found
- UAT Sign-Off document

**Ends with**: 🎯 **Mentor Review 3** (UAT Sign-Off, 05/08)

---

### Phase 4: Defense Preparation + Golive (06/08 – 23/08)

**Duration**: 2.5 weeks (Sprint 7)

**Goals**:
- Final documentation polish
- Demo rehearsal
- Defense presentation preparation
- Final code freeze and packaging

**Deliverables**:
- Final Report
- Defense Presentation slides
- Recorded demo video (backup)
- All required documents submitted

**Ends with**: 🎯 **Thesis Defense** (23/08) — academic evaluation

---

## Mentor Review Milestones

Four critical milestones drive the project schedule:

| Milestone | Date | Audience | Format | Demo state required |
|---|---|---|---|---|
| **Mentor Review 1** | 17/05 | FSoft Mentor | Workshop presentation | Slides + research findings |
| **Mentor Review 2** | 27/07 | FSoft Mentor | Full demo + tech spec | Working software, all features |
| **Mentor Review 3** | 05/08 | FSoft Mentor | UAT sign-off | UAT passed, signed off |
| **Thesis Defense** | 23/08 | School Panel + Supervisor | Defense presentation | Polished demo + Final Report |

**Implication**: Working software must be **demonstrable by 27/07** (Mentor Review 2). UAT cannot find major architecture issues — those must be resolved earlier.

---

## Team Roles and Responsibilities

The team operates with three work streams. Each member has a primary role plus a secondary/support area to ensure cross-coverage.

| Member | Primary | Secondary | Sprint deliverable focus |
|---|---|---|---|
| **Trần Ngọc Quý Long** | BE Lead / Architect | DevOps · Integration | .NET solution, Bot Framework, Docker, Azure OpenAI SDK |
| **TBD (SAP-1)** | SAP Lead — CDS / AMDP | OData modeling | CDS Aggregation Views, AMDP procedures, RAP service binding |
| **TBD (SAP-2)** | SAP — RAP / Z-Objects | SAP authorization | Z-Tables, RAP behavior pool, PFCG roles, background jobs |
| **TBD (AI-1)** | AI Lead — LLM / Function Calling | Prompt evaluation | Function registry, prompt design, Vietnamese tuning |
| **TBD (AI-2)** | AI/FE — Adaptive Cards | UAT + documentation | Card templates, chart rendering, manifest, user manual |

> ⚠️ Names for SAP-1, SAP-2, AI-1, AI-2 should be assigned by the team leader. Recommended to match each member's strongest pre-existing skill to reduce ramp-up time.

### Cross-cutting responsibilities

- **All members**: Code review, testing, documentation contributions, sprint ceremonies
- **BE Lead**: Final accountability for integration, release, defense demo readiness
- **AI-2 (or rotating)**: Sprint retrospective facilitation

---

## Methodology

### Phased + Scrum hybrid

- **Phased delivery**: Aligned with school + FSoft mentor calendar
- **2-week sprints**: Within Phase 2 (Realization)
- **Compressed cycles**: Phase 3 (UAT) is 1-week mini-sprint
- **Final phase**: Defense prep is project closeout, not feature work

### Backlog management

- All tasks tracked in GitHub Projects (kanban view)
- Issues labeled by role (be/sap/ai/fe) and sprint
- Estimation: T-shirt sizes (S/M/L/XL)

### No new features after Sprint 5

After Sprint 5 (Phase 2 end), NO new features. Only:
- Bug fixes (UAT findings)
- Documentation polish
- Demo rehearsal

### Incremental documentation

Technical Specification, User Manual, and Test documentation are **not** dumped into the last sprint. Each sprint writes the section corresponding to its deliverables:

| Sprint | Tech Spec section written | User Manual artifact |
|---|---|---|
| Sprint 1 | Architecture overview · Tech stack | (none — infra) |
| Sprint 2 | Bot service · Function calling design | Screenshots: install bot |
| Sprint 3 | SAP integration · CDS/AMDP/OData | Screenshots: queries + KPI cards |
| Sprint 4 | Workflow actions · AI insights | Screenshots: release/reject flows |
| Sprint 5 | Scheduled reports · PDF pipeline | Screenshots: reports + emails |
| Sprint 6 | (review & polish) | (review & polish) |

This avoids the documentation-crunch failure mode common to capstone projects.

---

## Sprint Ceremonies

### Sprint Planning (1 hour, Monday Week 1)
1. Review previous sprint outcomes (10 min)
2. Confirm sprint goal (10 min)
3. Assign tasks per role (30 min)
4. Identify dependencies and blockers (10 min)

### Daily Standup (15 min, Mon–Fri 09:00)
- What I did yesterday
- What I'll do today
- Any blockers?

### Sprint Review / Demo (1 hour, Friday Week 2)
1. Demo working features (40 min)
2. Supervisor feedback (15 min)
3. Update on overall progress (5 min)

### Sprint Retrospective (30 min, after Review)
1. What went well? (10 min)
2. What could improve? (10 min)
3. Action items for next sprint (10 min)

### Mentor Review (timing per phase end)
- Phase 1 end: Workshop presentation
- Phase 2 end: Demo full software + tech spec walkthrough
- Phase 3 end: UAT results + sign-off
- Format: as agreed with mentor (Google Meet, in-person, or hybrid)

---

## Definition of Done

For a task to be **DONE**:
- [ ] Code committed and pushed
- [ ] Pull Request created with proper template
- [ ] At least 1 approval from another team member
- [ ] CI checks pass (build, tests, lint)
- [ ] PR merged to `develop`
- [ ] Issue closed with reference to PR
- [ ] Documentation updated if applicable
- [ ] Demo-ready in working state

For a **sprint** to be DONE:
- [ ] All committed tasks have status "Done"
- [ ] Sprint demo delivered
- [ ] Retrospective conducted
- [ ] Sprint summary written in `docs/sprints/sprint-N-summary.md`
- [ ] Corresponding Tech Spec section drafted

For a **phase** to be DONE:
- [ ] All sprint deliverables completed
- [ ] Phase deliverables ready for mentor review
- [ ] Mentor review conducted
- [ ] Feedback documented and triaged

---

## Quality Metrics

### Vietnamese natural-language accuracy

**Definition**: Percentage of test queries for which the bot produces (a) the correct function call AND (b) correct parameter values.

**Test set**:
- Minimum 100 Vietnamese queries + 50 English queries
- Covers all 15+ functions with parameter variations
- Annotated ground truth (function name + expected parameters) maintained in `tests/nl-accuracy/golden.jsonl`
- Annotated by AI Team, reviewed by full team

**Measurement**:
- Run automated test harness against each model version / prompt iteration
- Pass criteria: ≥ 85% function-selection accuracy + ≥ 80% parameter-extraction accuracy
- Logged per sprint in `docs/quality/nl-accuracy-history.md`

### Response latency

| Query type | Target P95 latency |
|---|---|
| Cached query (Redis hit) | < 2 seconds |
| Cached KPI dashboard | < 3 seconds |
| Live SAP query (Cloud Connector path) | < 8 seconds first call, < 4s subsequent |
| PDF generation | < 15 seconds |
| AI insight generation | < 6 seconds |

Measured via Application Insights / structured logs in production-like environment.

### Code quality gates (CI)

- Build passes
- Unit tests pass (target ≥ 60% coverage in service layer)
- No critical-severity static analysis issues (SonarCloud free tier or built-in)
- No merge to `main` without sprint review approval

---

## Sprint Schedule

| Sprint | Phase | Dates | Theme | Major Milestone |
|---|---|---|---|---|
| Pre-Sprint | Phase 1 | 16/05 – 17/05 | Kick-off | Workshop with mentor |
| Sprint 1 | Phase 2 | 19/05 – 01/06 | Foundation + Spikes | Echo bot in Emulator, Cloud Connector requested, all spikes done |
| Sprint 2 | Phase 2 | 02/06 – 15/06 | Core Integrations | Bot live in Teams + Mock SAP + first function call |
| Sprint 3 | Phase 2 | 16/06 – 29/06 | Feature Build I | SO queries + KPIs on real SAP data |
| Sprint 4 | Phase 2 | 30/06 – 13/07 | Feature Build II + Reports | Workflow actions, AI Bonus, Hangfire scheduled jobs |
| Sprint 5 | Phase 2 | 14/07 – 27/07 | PDF + Polish | PDF/email distribution, full polish, Mentor Review 2 ready |
| Sprint 6 | Phase 3 | 30/07 – 05/08 | UAT | UAT executed and signed off |
| Sprint 7 | Phase 4 | 06/08 – 23/08 | Defense Prep | Final docs + rehearsal + defense |

**Critical dates**:
- 🎯 17/05 — Mentor Review 1
- 🎯 27/07 — Mentor Review 2 (full demo)
- 🎯 05/08 — Mentor Review 3 (UAT Sign-Off)
- 🎯 23/08 — Thesis Defense

**Key change from v2.0**: Sprint 1 lightened (Teams sideload moved to Sprint 2), Sprint 4 absorbs Hangfire + scheduled jobs (previously Sprint 5), Sprint 5 focused on PDF + email + polish only.

---

## Sprint Details

### Pre-Sprint — Preparation & Explore (Phase 1)

**Dates**: 16/05 – 17/05 (2 days)
**Sprint Goal**: Align team, research SAP, prepare for FSoft mentor meeting.

#### Deliverables by role

**All members**
- [ ] Attend project kick-off meeting
- [ ] Agree on team distribution (BE, SAP×2, AI×2)
- [ ] Sign off on initial scope from Capstone Proposal

**BE Lead (Long)**
- [ ] Prepare workshop slides intro
- [ ] Architecture overview slide (1-2 slides)
- [ ] Demo scenario walkthrough

**SAP Team**
- [ ] Research SAP SD module functions
- [ ] Identify standard transactions (VA01, VA03, VA05, VL02N, VF03)
- [ ] Note candidate Z-objects and CDS views

**AI Team**
- [ ] Research LLM function calling patterns
- [ ] Identify candidate intents and queries
- [ ] Note AI risks (hallucination, Vietnamese quality)

#### Phase 1 Demo Criteria (Mentor Review 1)
- ✓ Workshop slides presented
- ✓ Team can articulate architecture, scope, timeline
- ✓ Initial SAP research documented
- ✓ Questions for mentor prepared

---

### Sprint 1 — Foundation + Technical Spikes (Phase 2)

**Dates**: 19/05 – 01/06
**Sprint Goal**: Establish dev infrastructure, complete all technical spikes to de-risk unknown technologies, and submit Cloud Connector access request.

#### Technical Spikes (mandatory, Week 1)

Each spike is timeboxed to **1 day** and produces a working demo + 1-page learnings doc in `docs/spikes/`.

- [ ] **Spike A** (AI-1): Azure OpenAI function calling with sample schema — call from .NET, parse response
- [ ] **Spike B** (BE Lead): Bot Framework SSO in Teams — token acquisition and refresh flow
- [ ] **Spike C** (SAP-1): RAP service binding for analytical CDS — expose a trivial CDS view as OData V4
- [ ] **Spike D** (SAP-2): Cloud Connector connectivity test — verify reachability from a sandbox cloud VM (if access granted)

#### Sprint deliverables by role

**BE Lead**
- [ ] Setup .NET solution skeleton (8 projects scaffolded)
- [ ] Configure Docker Compose (PostgreSQL + Redis)
- [ ] Bot Framework SDK integration in `AISO.Bot`
- [ ] Echo bot working in Bot Framework Emulator
- [ ] ngrok / Dev Tunnels setup for public endpoint
- [ ] GitHub Actions CI pipeline (build + test)
- [ ] Tech Spec section: Architecture overview drafted

**SAP Team (SAP-1 + SAP-2)**
- [ ] Install Eclipse ADT on all SAP team machines
- [ ] Connect to S40 system (UCC TUM) via SAP GUI + ADT
- [ ] Explore standard SD tables (VBAK, VBAP, KNA1, etc.)
- [ ] Run sample transactions (VA01, VA03, VA05) — document data shape
- [ ] Setup abapGit, link to repo `sap/abap/` subfolder
- [ ] Create Z-package `Z_AISO` for AISO objects
- [ ] Design first Z-tables (DDL only, no implementation yet)
- [ ] 🔴 **Submit Cloud Connector access request to UCC support** — highest priority, lead time 2-4 weeks

**AI Team (AI-1 + AI-2)**
- [ ] Apply for Azure OpenAI access (use student credit)
- [ ] Practice function calling in OpenAI Playground (5 sample functions)
- [ ] Setup Teams Toolkit on AI-2 machine
- [ ] Design first 3 Adaptive Card mockups (welcome, help, error) — Figma or Adaptive Cards Designer

#### Sprint 1 Demo criteria
- ✓ Echo bot replies in Bot Framework Emulator
- ✓ All 4 spikes completed with learnings docs committed
- ✓ SAP team can run VA03 in S40 and explain data structure
- ✓ Cloud Connector request submitted with ticket reference
- ✓ Azure OpenAI access request submitted

#### Risks
- 🚨 Cloud Connector request may take 2-4 weeks → mock-first dev for Sprints 2-3
- 🚨 Azure OpenAI approval can take 1-3 days → have fallback (e.g. OpenAI direct API, Gemini)
- 🚨 SAP UCC system access dependent on credentials → verify Day 1

---

### Sprint 2 — Core Integrations (Phase 2)

**Dates**: 02/06 – 15/06
**Sprint Goal**: First end-to-end "magical moment" — user query in **live Microsoft Teams** returns mocked SAP data via Azure OpenAI function calling.

#### Sprint deliverables by role

**BE Lead**
- [ ] Azure AD app registration for bot + Teams permissions
- [ ] Azure Bot Service resource created
- [ ] Connect Azure AD credentials to bot service
- [ ] Test bot sideload in Microsoft Teams (uses M365 Developer Program tenant)
- [ ] `AISO.Persistence`: EF Core setup, `AppDbContext`, first migration
- [ ] First entities: `UserMapping`, `AuditLog`
- [ ] `AISO.SapIntegration`: `ISapClient` interface + `MockSapClient` with hardcoded sample data
- [ ] `AISO.AiOrchestration`: Azure OpenAI SDK integration
- [ ] First function `getSalesOrders` end-to-end (bot → AI → mock SAP → response)
- [ ] Health check endpoint `/health`
- [ ] Tech Spec section: Bot service + function calling design

**SAP Team**
- [ ] Create first Z-tables (`Z_AISO_KPI_LOG`, `Z_AISO_KPI_SNAP`, `Z_AISO_AUDIT`)
- [ ] First CDS Basic View: `ZI_AISO_SALES_ORDER` (join VBAK + VBAP)
- [ ] First CDS Composite View: `ZC_AISO_SO_WITH_DELIVERY`
- [ ] Cloud Connector follow-up with UCC; if granted, complete initial config

**AI Team**
- [ ] Azure OpenAI resource created (Japan East / Australia East — proximity to Vietnam)
- [ ] Deploy GPT-4o model
- [ ] Function registry first version (5 functions defined with JSON schema)
- [ ] System prompt v1 (English only)
- [ ] First 5 Adaptive Card templates designed and rendered
- [ ] Teams app manifest v1 + icons

#### Sprint 2 Demo criteria
- ✓ User types in Teams: "show recent orders" → bot returns formatted card with mock data
- ✓ Function call sequence visible in logs (request + chosen function + parameters + response)
- ✓ SAP team shows VBAK/VBAP data via CDS view in ADT
- ✓ Teams app installable with proper icon

---

### Sprint 3 — Feature Build I: Real SAP Data Flow (Phase 2)

**Dates**: 16/06 – 29/06
**Sprint Goal**: Implement SO query and KPI features with real SAP data flowing from S40.

#### Sprint deliverables by role

**BE Lead**
- [ ] Real `SapClient` implementation (HttpClient + Polly retry + CSRF token handling)
- [ ] OData query builder (filter, top, skip, expand)
- [ ] Switch functions from `MockSapClient` to real `SapClient`
- [ ] Bot dialog flow refactoring (state management, turn handlers)
- [ ] SSO `OAuthPrompt` integration in Teams
- [ ] Token caching in Redis with refresh logic
- [ ] Application Insights integration (basic telemetry)
- [ ] Tech Spec section: SAP integration architecture, CDS/AMDP/OData walkthrough

**SAP Team**
- [ ] First Analytical CDS View: `ZR_AISO_REVENUE_CUBE` with `@Analytics.dataExtraction.enabled: true`
- [ ] First AMDP procedure: `ZCL_AISO_AMDP_REVENUE` (SQLScript on HANA)
- [ ] CDS views for: SO Aging, Delivery KPI, AR Aging (4 KPI domains total)
- [ ] RAP service definition `ZSD_AISO_SALES_ORDER`
- [ ] RAP service binding `ZSB_AISO_SALES_ORDER_V4`
- [ ] OData service activated and consumable externally (verified via Postman)

**AI Team**
- [ ] Function definitions expanded to 10 functions
- [ ] System prompt v2 (Vietnamese + English support)
- [ ] **Vietnamese test set v1**: 50+ queries with ground truth annotation in `tests/nl-accuracy/golden.jsonl`
- [ ] Adaptive Cards expanded: 8-10 templates including KPI cards with server-rendered chart images
- [ ] Card rendering tested on Teams desktop + mobile

#### Sprint 3 Demo criteria
- ✓ User queries "show SO 5000123" → bot returns REAL data from SAP via Cloud Connector
- ✓ Revenue dashboard renders with chart image
- ✓ Vietnamese query works on at least 80% of test set
- ✓ SSO sign-in flow functional in Teams

#### Scope decision gate
- If real SAP path not working by mid-Sprint 3 → continue with mock for Sprints 3-4, attempt switch in Sprint 5
- Document fallback decision and rationale in retro

---

### Sprint 4 — Feature Build II + Workflows + Scheduled Reports (Phase 2)

**Dates**: 30/06 – 13/07
**Sprint Goal**: Complete workflow actions, AI Bonus features, and Hangfire-driven scheduled reports.

#### Sprint deliverables by role

**BE Lead**
- [ ] All 15+ function handlers wired in `AISO.AiOrchestration`
- [ ] Workflow function handlers: release, reject, forward, substitution
- [ ] Attachment management (view, upload, download)
- [ ] Authorization check before action execution
- [ ] Audit log entries for every action
- [ ] **`AISO.Scheduling`: Hangfire setup with PostgreSQL persistence** _(moved from Sprint 5)_
- [ ] **Weekly report job (Monday 9 AM)** _(moved from Sprint 5)_
- [ ] **Daily alert check job (revenue drop, delivery overdue)** _(moved from Sprint 5)_
- [ ] AI Bonus: Insights generation pipeline (aggregation → LLM → narrative output)
- [ ] Pseudonymization layer for any data sent to LLM
- [ ] Hallucination detection (validate referenced SO numbers exist)
- [ ] Tech Spec section: Workflow actions, AI insights, scheduling

**SAP Team**
- [ ] RAP Behavior Definitions for SO actions (release, reject) — this is where RAP behavior pool kicks in
- [ ] RAP Behavior Implementations (ABAP classes)
- [ ] PFCG roles: `ZROLE_AISO_BOT_VIEWER`, `ZROLE_AISO_BOT_RELEASER`
- [ ] Authorization objects setup
- [ ] Bot service user (`ZBOT_USER`) configured
- [ ] Substitution management table + service in SAP
- [ ] **Background job for KPI refresh (6-hour cycle, SM37 scheduled)** _(consolidated here)_

**AI Team**
- [ ] All 15+ function definitions complete with schemas
- [ ] Action confirmation prompts (release/reject/forward)
- [ ] Reason code cards (rejection)
- [ ] User search cards (forwarding)
- [ ] Substitution setup cards
- [ ] Insights prompt design + Adaptive Card template
- [ ] **Vietnamese test set v2**: expand to 100+ queries + 50 English
- [ ] Test harness runs in CI

#### Sprint 4 Demo criteria
- ✓ User releases SO with comment, action reflected in SAP (verifiable in VA02/VA03)
- ✓ User rejects SO with reason from dropdown
- ✓ Forward to colleague works
- ✓ Substitution setup creates rule in SAP
- ✓ AI insights generation produces analytical narrative on a sample report
- ✓ Scheduled weekly report job runs on demand (Hangfire dashboard visible)
- ✓ All 6 KPI cards render with real data
- ✓ NL accuracy on Vietnamese ≥ 85% on test set v2

---

### Sprint 5 — PDF Distribution + Polish (Phase 2)

**Dates**: 14/07 – 27/07
**Sprint Goal**: PDF generation, email distribution, end-to-end polish. Sprint end = Mentor Review 2 ready.

#### Sprint deliverables by role

**BE Lead**
- [ ] `AISO.Reporting`: Azure Functions project for PDF generation
- [ ] Puppeteer-based PDF rendering from Adaptive Card → HTML → PDF
- [ ] Monthly consolidated PDF template (cover page, KPI sections, charts)
- [ ] Microsoft Graph email send integration
- [ ] On-demand PDF export action button on every detail card
- [ ] Channel subscription management (subscribe/unsubscribe commands)
- [ ] Alert threshold configuration (via admin command or env config)
- [ ] Azure Key Vault integration (move secrets out of env vars) — _Should Have_, cut if behind
- [ ] Final logging and error handling pass
- [ ] Tech Spec section: PDF pipeline, distribution, finalization

**SAP Team**
- [ ] Cleanup job for audit log (retention policy)
- [ ] Performance tuning of slow analytical queries (index advisor, view optimization)
- [ ] Cloud Connector configuration finalized if still pending
- [ ] All SAP code reviewed and refactored
- [ ] SAP documentation finalized (transport list, deployment guide)

**AI Team**
- [ ] Weekly report card template (finalized layout)
- [ ] Alert notification cards
- [ ] PDF export action buttons wired to bot commands
- [ ] Email body templates (Vietnamese + English)
- [ ] Comprehensive test re-run on test set v2
- [ ] Edge case handling: empty results, SAP timeout, LLM hallucination fallback
- [ ] Subscription management cards

**All members (parallel)**
- [ ] **Finalize Technical Specification** (sections already drafted per sprint; final compilation + review)
- [ ] **User Manual** finalized with all screenshots from previous sprints
- [ ] **Testing documentation** (test scenarios + results compiled)
- [ ] Sprint 5 demo dry run with full feature walk-through

#### Sprint 5 Demo criteria (= Mentor Review 2 demo)
- ✓ Full end-to-end demo with ALL features working
- ✓ Weekly report appears in subscribed channel
- ✓ Monthly PDF generated and emailed
- ✓ On-demand PDF works from any card
- ✓ All cards working on desktop + mobile
- ✓ Vietnamese accuracy ≥ 85% (measured on test set v2)
- ✓ Documentation complete: Technical Spec + User Manual + Testing

#### 🎯 Phase 2 Ends: Mentor Review 2 (27/07)
- Demo full software to FSoft mentor
- Present Technical Specification
- Show test results
- Walk through User Manual
- Mentor feedback collected

---

### Sprint 6 — UAT (Phase 3)

**Dates**: 30/07 – 05/08 (1 week — short sprint)
**Sprint Goal**: User Acceptance Testing executed, bugs fixed, UAT sign-off obtained.

#### Sprint deliverables by role

**All members (UAT execution)**
- [ ] UAT scenarios prepared (from Functional Spec)
- [ ] UAT executed with mentor + supervisor + invited users
- [ ] UAT findings logged in `docs/uat/findings-log.md`
- [ ] Critical bugs fixed
- [ ] Non-critical bugs logged for post-defense or accepted

**BE Lead**
- [ ] Lead UAT triage meeting
- [ ] Hotfix critical bugs
- [ ] Re-deploy with fixes

**SAP Team**
- [ ] Hotfix SAP-side bugs
- [ ] Re-test SAP services

**AI Team**
- [ ] Hotfix prompts if accuracy issues found
- [ ] Re-test cards on devices

**All**
- [ ] UAT Sign-Off document prepared
- [ ] UAT report written

#### Sprint 6 Demo criteria (= Mentor Review 3)
- ✓ All critical UAT bugs fixed
- ✓ UAT Sign-Off signed by mentor
- ✓ UAT report documents all findings + resolutions
- ✓ Software in stable state ready for defense prep

#### 🎯 Phase 3 Ends: Mentor Review 3 — UAT Sign-Off (05/08)

---

### Sprint 7 — Defense Preparation (Phase 4)

**Dates**: 06/08 – 23/08 (2.5 weeks)
**Sprint Goal**: Defense delivery — polished, rehearsed, confident.

#### Sprint deliverables by role

**All members**
- [ ] Final defense rehearsals (3+ runs)
- [ ] Q&A practice with mock panel
- [ ] Backup plans for demo failures
- [ ] Slides finalized and approved by supervisor
- [ ] All deliverables packaged for submission
- [ ] Defense day attire and logistics planned

**BE Lead**
- [ ] Final bug fixes (only critical)
- [ ] Production-like environment for defense
- [ ] Backup demo recorded
- [ ] Final Report (lead writer, all contribute)

**SAP Team**
- [ ] SAP system warm-up scripts for demo
- [ ] Test data refresh before defense
- [ ] Backup SAP query examples ready

**AI Team**
- [ ] Demo conversation script with planned queries
- [ ] Backup queries if Azure OpenAI rate-limited
- [ ] Defense-day card library locked
- [ ] Demo video recorded (final, high quality)

#### Sprint 7 Demo criteria (= Thesis Defense, 23/08)
- ✓ Defense delivered successfully
- ✓ Demo executed within time limit
- ✓ All questions handled professionally
- ✓ Pass academic evaluation

#### Critical reminders for Phase 4
- 🔴 **NO new features** — only fixes and polish
- 🔴 Have **offline backup** of demo (recorded video)
- 🔴 **Network outage plan** for defense day
- 🔴 **Two laptops** ready (primary + backup)

#### 🎯 Phase 4 Ends: Thesis Defense (23/08)

---

## Velocity Tracking

### Per-sprint velocity log

| Sprint | Phase | Planned | Completed | Velocity |
|---|---|---|---|---|
| Pre-Sprint | 1 | [TBD] | [TBD] | [TBD] |
| Sprint 1 | 2 | | | |
| Sprint 2 | 2 | | | |
| Sprint 3 | 2 | | | |
| Sprint 4 | 2 | | | |
| Sprint 5 | 2 | | | |
| Sprint 6 | 3 | | | |
| Sprint 7 | 4 | | | |

### Capacity calculation

- Team size: 5
- Hours per week per member: 20 (part-time student)
- Total team hours per sprint (2 weeks): 5 × 20 × 2 = **200 hours/sprint**
- Account for ceremonies (~10 hours/sprint): **190 net hours/sprint**

### Estimation guide (T-shirt sizes)
- **S** (Small): 2-4 hours
- **M** (Medium): 8-16 hours (1-2 days)
- **L** (Large): 16-32 hours (2-4 days)
- **XL** (Extra Large): 32+ hours (>4 days, consider splitting)

---

## Risk Management

### Critical risks specific to compressed timeline

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| Phase 2 work doesn't finish by 27/07 | Medium | **HIGH** | Cut AI Bonus to stretch goal early; lock scope after Sprint 3 |
| UAT finds blocking bugs | Medium | **HIGH** | Pre-UAT internal QA in Sprint 5; have buffer days |
| Mentor unavailable for review | Low | High | Schedule mentor reviews in advance; have backup async review |
| **Cloud Connector access delayed/denied** | **High** | **High** | Submit request Sprint 1 Day 1; mock-first dev for Sprints 2-3; document fallback (VPN tunnel, reverse proxy, or on-prem bot deploy) |
| Azure OpenAI quota | Medium | Medium | Use student credits wisely, have OpenAI direct / Gemini fallback |
| Team member dropout | Low | High | Cross-training, knowledge sharing |
| S40 system outage during demo | Low | High | Pre-cache test data, recorded demo backup |
| Vietnamese accuracy below 85% | Medium | Medium | Iterate prompts in Sprint 3-4; expand test set incrementally |
| Authorization complexity (RAP behavior) | Medium | Medium | Simplify scope in Sprint 4 (release + reject only); document deferred actions |
| Azure Functions cold start affects PDF UX | Low | Medium | Use Premium plan or warm-up ping; show "generating..." card |

### Risk review cadence
- Risk register reviewed every sprint retrospective
- Critical risks (Prob High + Impact High) reviewed weekly
- Escalation: BE Lead → Supervisor for blocking risks

### Contingency budget

Reserve **20% of each sprint** for:
- Unplanned bugs
- Integration issues
- Re-work from sprint review feedback

---

## Scope Management

Given compressed timeline, scope must be actively managed.

### Must Have (cannot cut)
- All 4 main requirements (queries, KPIs, workflows, scheduled reports)
- Microsoft Teams integration
- Azure OpenAI function calling
- SAP integration (via OData V4)
- Microsoft Graph email send
- PDF generation
- Vietnamese + English NL support

### Should Have (cut if behind by Sprint 3)
- AI Bonus features (insights generation, anomaly detection)
- Cloud Connector (fallback: VPN tunnel or on-prem bot deploy)
- Application Insights (basic logging sufficient)
- Azure Key Vault (env vars acceptable for demo)
- Substitution management (release + reject only as fallback)

### Could Have (skip without consequence)
- Voice input (Teams has native, no extra work needed)
- Multi-language beyond VN/EN
- Advanced caching strategies
- Performance optimizations beyond basic
- Hangfire dashboard exposed via admin command

### Won't Have (out of scope)
- Production deployment
- Load testing
- Multi-tenant support
- Full GDPR compliance audit
- Real-time KPI (we have 6-hour refresh cycle)

### Scope decision gates

| Decision point | Trigger | Action |
|---|---|---|
| End of Sprint 1 | Cloud Connector not approved? | Continue mock path, set deadline Sprint 3 mid for switch |
| After Sprint 2 | Behind on integrations? | Reduce KPI features in Sprint 3 |
| After Sprint 3 | Behind on features? | Cut AI Bonus, focus core |
| After Sprint 4 | Behind on workflows? | Cut substitution, focus release/reject |
| After Sprint 5 | Behind on reports? | Cut alerts, keep only scheduled reports |

---

## Appendix: Templates

### Sprint Planning Template

```markdown
# Sprint N Planning — [Sprint Theme]

**Date**: [Date]
**Phase**: [Phase number]
**Participants**: [Names]

## Previous sprint review
- Velocity: X tasks completed
- Carryover: [tasks not completed]

## This sprint goal
[One sentence goal]

## Tasks assigned
- BE: [list with assignees]
- SAP: [list with assignees]
- AI: [list with assignees]

## Dependencies
- [List]

## Risks identified
- [List]

## Definition of "demo-ready" for this sprint
- [Criteria]

## Phase progress
- This sprint takes us to: X% of Phase Y
```

### Sprint Retro Template

```markdown
# Sprint N Retrospective

**Date**: [Date]
**Facilitator**: [Name (rotates)]

## What went well
- [Items]

## What could improve
- [Items]

## Action items for next sprint
1. [Action] — owner: [Name] — by: [Date]
2. ...

## Velocity
- Completed: X/Y tasks

## Phase health check
- On track for [next mentor review]? [Yes/No, why]

## Risk register changes
- [New / updated / closed risks]
```

### Mentor Review Template

```markdown
# Mentor Review N

**Date**: [Date]
**Phase ending**: [Phase number]
**Attendees**: [Mentor, supervisor, team]

## Demo agenda
1. [Item with time allocation]
2. ...

## Demo state
- Features ready: [list]
- Known issues: [list]

## Questions for mentor
1. [Question]
2. ...

## Mentor feedback
- [Captured feedback]

## Action items from mentor
1. [Action] — owner: [Name] — by: [Date]
```

---

**Document version**: 3.0
**Last updated**: 2026-06-06
**Maintained by**: BE Lead (Trần Ngọc Quý Long)

**Changelog v2.0 → v3.0**:
- Added Team Roles and Responsibilities section with per-member allocation
- Added Quality Metrics section (NL accuracy definition, latency targets, code quality)
- Added Incremental Documentation strategy (Tech Spec written per sprint)
- Added Technical Spikes to Sprint 1 (function calling, SSO, RAP, Cloud Connector)
- Sprint 1 lightened: Azure Bot Service + Teams sideload moved to Sprint 2
- Sprint 4 absorbs Hangfire + scheduled jobs (previously Sprint 5)
- Sprint 5 reduced to PDF + email + polish
- Cloud Connector access elevated to High/High risk with Sprint 1 Day 1 submission
- Scope decision gate added for Cloud Connector at end of Sprint 1
