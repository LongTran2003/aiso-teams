# Sprint Plan — AISO-Teams

**Project**: AISO-Teams (AI-Powered Microsoft Teams Chatbot for SAP Sales Order Management)
**Duration**: May 2026 – August 2026 (~14 weeks)
**Methodology**: Phased delivery with Scrum-inspired 2-week sprints
**Team**: 5 members (1 BE Lead + 2 SAP + 2 AI/FE)
**Mentor**: FSoft Mentor (TBD) + School Supervisor: Thầy Nguyễn Bá Lê

---

## Table of Contents

- [Project Phases Overview](#project-phases-overview)
- [Mentor Review Milestones](#mentor-review-milestones)
- [Methodology](#methodology)
- [Sprint Ceremonies](#sprint-ceremonies)
- [Definition of Done](#definition-of-done)
- [Sprint Schedule](#sprint-schedule)
- [Sprint Details](#sprint-details)
- [Velocity Tracking](#velocity-tracking)
- [Risk Management](#risk-management)
- [Scope Management](#scope-management)

---

## Project Phases Overview

The project follows a **4-phase delivery model** aligned with FSoft mentor reviews and the academic defense calendar.

```
Phase 1                  Phase 2                                Phase 3       Phase 4
Preparation +            Realization                            UAT           Defense
Explore                  (Development)                                        (Golive)
─────────                ──────────────────────────────────     ───────       ─────────
16/05 — 17/05            19/05 — 27/07                          30/07 — 05/08  23/08
2 days                   10 weeks (5 sprints)                   1 week         1 day
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
- Write Technical Specification
- Complete Testing
- Write User Manual

**Deliverables**:
- Working bot in Microsoft Teams
- All 15+ functions implemented
- Technical Specification document
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

For a **phase** to be DONE:
- [ ] All sprint deliverables completed
- [ ] Phase deliverables ready for mentor review
- [ ] Mentor review conducted
- [ ] Feedback documented and triaged

---

## Sprint Schedule

| Sprint | Phase | Dates | Theme | Major Milestone |
|---|---|---|---|---|
| Pre-Sprint | Phase 1 | 16/05 – 17/05 | Kick-off | Workshop with mentor |
| Sprint 1 | Phase 2 | 19/05 – 01/06 | Foundation | Echo bot in Teams |
| Sprint 2 | Phase 2 | 02/06 – 15/06 | Core Integrations | Mock SAP + Function calling |
| Sprint 3 | Phase 2 | 16/06 – 29/06 | Feature Build I | SO queries + KPIs |
| Sprint 4 | Phase 2 | 30/06 – 13/07 | Feature Build II | Workflow actions + AI Bonus |
| Sprint 5 | Phase 2 | 14/07 – 27/07 | Reports + Polish | All features done, ready for Mentor Review 2 |
| Sprint 6 | Phase 3 | 30/07 – 05/08 | UAT | UAT executed and signed off |
| Sprint 7 | Phase 4 | 06/08 – 23/08 | Defense Prep | Final docs + rehearsal + defense |

**Critical dates**:
- 🎯 17/05 — Mentor Review 1
- 🎯 27/07 — Mentor Review 2 (full demo)
- 🎯 05/08 — Mentor Review 3 (UAT Sign-Off)
- 🎯 23/08 — Thesis Defense

---

## Sprint Details

### Pre-Sprint — Preparation & Explore (Phase 1)

**Dates**: 16/05 – 17/05 (2 days)
**Sprint Goal**: Align team, research SAP, prepare for FSoft mentor meeting.

#### Deliverables by role

**All members**
- [ ] Attend project kick-off meeting
- [ ] Agree on team distribution (BE, SAP×2, AI×2, with FE absorbed)
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

### Sprint 1 — Foundation (Phase 2)

**Dates**: 19/05 – 01/06
**Sprint Goal**: Establish development infrastructure and verify end-to-end bot communication.

#### Sprint deliverables by role

**BE Lead**
- [ ] Setup .NET solution with 8 projects
- [ ] Configure Docker Compose (PostgreSQL + Redis)
- [ ] Bot Framework SDK integration
- [ ] Echo bot working in Bot Framework Emulator
- [ ] Azure AD app registration
- [ ] Azure Bot Service resource created
- [ ] Connect Azure AD credentials to bot
- [ ] ngrok / Dev Tunnels setup for public endpoint
- [ ] Test bot sideload in Microsoft Teams

**SAP Team**
- [ ] Install Eclipse ADT on both machines
- [ ] Connect to S40 system (UCC TUM)
- [ ] Explore standard SD tables (VBAK, VBAP, KNA1, etc.)
- [ ] Run sample transactions (VA01, VA03, VA05)
- [ ] Setup abapGit, link to repo `sap/abap/` subfolder
- [ ] Create Z-package for AISO objects
- [ ] Plan first Z-tables (design only)

**AI Team**
- [ ] Apply for Azure OpenAI access
- [ ] Practice function calling in OpenAI Playground
- [ ] Setup Teams Toolkit
- [ ] Design first 3 Adaptive Card mockups (welcome, help, error)

#### Sprint 1 Demo criteria
- ✓ Echo bot replies in Microsoft Teams (real, not emulator)
- ✓ SAP team can run VA03 in S40 and screenshot
- ✓ Azure OpenAI access request submitted
- ✓ GitHub repo structure complete

#### Risks
- 🚨 Microsoft Teams sideload requires M365 tenant admin consent — use Developer Program tenant
- 🚨 Azure OpenAI approval can take 1-3 days
- 🚨 SAP UCC system access dependent on credentials

---

### Sprint 2 — Core Integrations (Phase 2)

**Dates**: 02/06 – 15/06
**Sprint Goal**: First end-to-end "magical moment" — user query in Teams returns mocked SAP data via Azure OpenAI function calling.

#### Sprint deliverables by role

**BE Lead**
- [ ] AISO.Persistence: EF Core setup, AppDbContext, first migration
- [ ] First entities: UserMapping, AuditLog
- [ ] AISO.SapIntegration: ISapClient interface
- [ ] MockSapClient with hardcoded sample data
- [ ] AISO.AiOrchestration: Azure OpenAI SDK integration
- [ ] First function: `getSalesOrders` end-to-end (bot → AI → mock SAP → response)
- [ ] Health check endpoint `/health`

**SAP Team**
- [ ] Create first Z-tables (Z_AISO_KPI_LOG, Z_AISO_KPI_SNAP, Z_AISO_AUDIT)
- [ ] First CDS Basic View: `ZI_AISO_SALES_ORDER` (join VBAK + VBAP)
- [ ] First CDS Composite View: `ZC_AISO_SO_WITH_DELIVERY`
- [ ] Begin Cloud Connector access request to UCC support

**AI Team**
- [ ] Azure OpenAI resource created (Japan East / Australia East)
- [ ] Deploy GPT-4o model
- [ ] Function registry first version (5 functions defined)
- [ ] System prompt v1 (English only)
- [ ] First 5 Adaptive Card templates designed
- [ ] Teams app manifest v1 + icons

#### Sprint 2 Demo criteria
- ✓ User types in Teams: "show recent orders" → bot returns formatted card with mock data
- ✓ Function call sequence visible in logs
- ✓ SAP team shows VBAK data via CDS view in ADT
- ✓ Teams app installable with proper icon

---

### Sprint 3 — Feature Build I (Phase 2)

**Dates**: 16/06 – 29/06
**Sprint Goal**: Implement SO query and KPI features with real SAP data flowing.

#### Sprint deliverables by role

**BE Lead**
- [ ] Real SapClient implementation (HttpClient + Polly retry + CSRF)
- [ ] OData query builder
- [ ] Switch functions from Mock to Real SAP path
- [ ] Bot dialog flow refactoring (state management, turn handlers)
- [ ] SSO OAuthPrompt integration
- [ ] Token caching
- [ ] Application Insights integration (basic telemetry)

**SAP Team**
- [ ] First Analytical CDS View: `ZR_AISO_REVENUE_CUBE`
- [ ] First AMDP procedure: `ZCL_AISO_AMDP_REVENUE`
- [ ] CDS views for: SO Aging, Delivery KPI, AR Aging
- [ ] First RAP Service: `ZSD_AISO_SALES_ORDER` + `ZSB_AISO_SALES_ORDER_V4`
- [ ] OData service activated and consumable externally

**AI Team**
- [ ] Function definitions expanded to 10 functions
- [ ] System prompt v2 (Vietnamese + English support)
- [ ] Vietnamese test cases: 20+ queries
- [ ] Adaptive Cards expanded: 8-10 templates including KPI cards with charts
- [ ] Card rendering tested on Teams desktop + mobile

#### Sprint 3 Demo criteria
- ✓ User queries "show SO 5000123" → bot returns REAL data from SAP
- ✓ Revenue dashboard renders with chart
- ✓ Vietnamese query works
- ✓ SSO sign-in flow functional in Teams

---

### Sprint 4 — Feature Build II + AI Bonus (Phase 2)

**Dates**: 30/06 – 13/07
**Sprint Goal**: Complete workflow actions and AI Bonus features.

#### Sprint deliverables by role

**BE Lead**
- [ ] All 15+ function handlers wired
- [ ] Workflow function handlers: release, reject, forward, substitution
- [ ] Attachment management (view, upload, download)
- [ ] Authorization check before action execution
- [ ] Audit log entries for every action
- [ ] AI Bonus: Insights generation pipeline (aggregation → LLM → output)
- [ ] Pseudonymization layer
- [ ] Hallucination detection

**SAP Team**
- [ ] RAP Behavior Definitions for SO actions (release, reject)
- [ ] RAP Behavior Implementations
- [ ] PFCG roles: ZROLE_AISO_BOT_VIEWER, ZROLE_AISO_BOT_RELEASER
- [ ] Authorization objects setup
- [ ] Bot service user (ZBOT_USER) configured
- [ ] Substitution management in SAP

**AI Team**
- [ ] All 15+ function definitions complete
- [ ] Action confirmation prompts
- [ ] Reason code cards (rejection)
- [ ] User search cards (forwarding)
- [ ] Substitution setup cards
- [ ] Insights prompt design + card template
- [ ] Test suite: 50+ Vietnamese, 30+ English

#### Sprint 4 Demo criteria
- ✓ User releases SO with comment, action reflected in SAP
- ✓ User rejects SO with reason from dropdown
- ✓ Forward to colleague works
- ✓ Substitution setup creates rule in SAP
- ✓ AI insights generation produces analytical narrative
- ✓ All 6 KPI cards render with real data

---

### Sprint 5 — Reports + Polish (Phase 2)

**Dates**: 14/07 – 27/07
**Sprint Goal**: Scheduled reports, PDF generation, final polish. End with full demo ready for Mentor Review 2.

#### Sprint deliverables by role

**BE Lead**
- [ ] AISO.Scheduling: Hangfire setup
- [ ] Weekly report job (Monday 9 AM)
- [ ] Daily alert check job (revenue drop, delivery overdue)
- [ ] Channel subscription management
- [ ] Alert threshold configuration
- [ ] AISO.Reporting: QuestPDF integration
- [ ] Monthly consolidated PDF template
- [ ] Microsoft Graph email send integration
- [ ] On-demand PDF export from any card
- [ ] Azure Key Vault integration (secrets)

**SAP Team**
- [ ] Background jobs for KPI refresh
- [ ] Cleanup job for audit log
- [ ] Performance tuning of slow queries
- [ ] Cloud Connector setup if available (or document direct fallback)
- [ ] All SAP code reviewed and refactored
- [ ] SAP documentation finalized

**AI Team**
- [ ] Weekly report card template
- [ ] Alert notification cards
- [ ] PDF export action buttons
- [ ] Email body templates (Vietnamese + English)
- [ ] Comprehensive test re-run after refinements
- [ ] Edge case handling polished
- [ ] Subscription management cards

**All members (parallel)**
- [ ] **Technical Specification document** (BE leads, all contribute)
- [ ] **User Manual** with all screenshots captured (AI team leads)
- [ ] **Testing documentation** (Test scenarios, results)

#### Sprint 5 Demo criteria (= Mentor Review 2 demo)
- ✓ Full end-to-end demo with ALL features working
- ✓ Weekly report appears in subscribed channel
- ✓ Monthly PDF generated and emailed
- ✓ All cards working on desktop + mobile
- ✓ Vietnamese accuracy >85%
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
- Hours per week per member: 20 (full-time student)
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
| SAP Cloud Connector blocked | High | Medium | Mock-first dev, document fallback |
| Azure OpenAI quota | Medium | Medium | Use credits wisely, have Gemini backup |
| Team member dropout | Low | High | Cross-training, knowledge sharing |
| S40 system outage | Low | High | Pre-cache test data, recorded demo |
| Vietnamese accuracy below 90% | Medium | Medium | Iterate prompts in Sprint 3-4 |
| Authorization complexity | Medium | Medium | Simplify in Sprint 4, document target |

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
- SAP integration (via OData)
- Microsoft Graph email send
- PDF generation

### Should Have (cut if behind by Sprint 3)
- AI Bonus features (insights generation, anomaly detection)
- Cloud Connector (fallback: direct HTTPS)
- Application Insights (basic logging sufficient)
- Azure Key Vault (env vars acceptable for demo)

### Could Have (skip without consequence)
- Voice input (Teams has native, no extra work needed)
- Multi-language beyond VN/EN
- Advanced caching strategies
- Performance optimizations beyond basic

### Won't Have (out of scope)
- Production deployment
- Load testing
- Multi-tenant support
- Full GDPR compliance audit

### Scope decision gates

| Decision point | Trigger | Action |
|---|---|---|
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
- FE: [list with assignees]

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

**Document version**: 2.0
**Last updated**: 2026-06-03
**Maintained by**: BE Lead (Trần Ngọc Quý Long)
**Change from v1.0**: Aligned with FSoft 4-phase delivery model and academic calendar (May-August 2026)
