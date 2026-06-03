# Sprint Plan — AISO-Teams

**Project**: AISO-Teams (AI-Powered Microsoft Teams Chatbot for SAP Sales Order Management)
**Duration**: May 2026 – September 2026 (20 weeks)
**Methodology**: Scrum-inspired, 2-week sprints
**Team**: 5 members (1 BE Lead + 2 SAP + 2 AI/FE)

---

## Table of Contents

- [Overview](#overview)
- [Methodology](#methodology)
- [Sprint Ceremonies](#sprint-ceremonies)
- [Definition of Done](#definition-of-done)
- [Sprint Schedule](#sprint-schedule)
- [Sprint Details](#sprint-details)
  - [Sprint 1 — Foundation](#sprint-1--foundation)
  - [Sprint 2 — Core Integrations](#sprint-2--core-integrations)
  - [Sprint 3 — Feature Build I](#sprint-3--feature-build-i)
  - [Sprint 4 — Feature Build II](#sprint-4--feature-build-ii)
  - [Sprint 5 — Workflow Actions](#sprint-5--workflow-actions)
  - [Sprint 6 — Scheduled Reports & Alerts](#sprint-6--scheduled-reports--alerts)
  - [Sprint 7 — PDF & Distribution](#sprint-7--pdf--distribution)
  - [Sprint 8 — AI Bonus & Polish](#sprint-8--ai-bonus--polish)
  - [Sprint 9 — Documentation & Rehearsal](#sprint-9--documentation--rehearsal)
  - [Sprint 10 — Defense Preparation](#sprint-10--defense-preparation)
- [Velocity Tracking](#velocity-tracking)
- [Risk Management](#risk-management)

---

## Overview

AISO-Teams is delivered in **10 sprints of 2 weeks each**, totaling 20 weeks. The project follows an iterative approach with working software at the end of each sprint. Each sprint has a clear theme and demo criteria.

### Sprint structure
- **Sprint length**: 2 weeks (Monday – Friday next week)
- **Sprint planning**: First Monday of sprint (1 hour)
- **Daily standup**: 15 min, Mon–Fri at 09:00 (via Teams)
- **Sprint review/demo**: Last Friday of sprint (1 hour)
- **Sprint retrospective**: Last Friday of sprint (30 min, after review)

### Phase mapping
| Phase | Sprints | Focus |
|---|---|---|
| Foundation | 1 | Infrastructure, setup, first end-to-end |
| Integration | 2 | All major components connected (mock + real) |
| Feature build | 3-5 | Implement business features iteratively |
| Reporting | 6-7 | Scheduled jobs, PDF, email distribution |
| Bonus & polish | 8 | AI insights, edge cases, refinement |
| Delivery prep | 9-10 | Documentation finalization, demo rehearsal, defense |

---

## Methodology

### Scrum-inspired (not pure Scrum)

We use Scrum patterns adapted for a capstone team:

- **Sprint backlog**: managed in GitHub Projects (kanban view)
- **Issues**: every task is a GitHub issue with labels (be/sap/ai/fe, priority, sprint)
- **Pull Requests**: all code changes via PR to `develop`
- **Estimation**: T-shirt sizes (S/M/L/XL) instead of story points (simpler for small team)
- **No dedicated Scrum Master**: BE Lead facilitates, rotates retrospective lead
- **No Product Owner**: supervisor + team consensus on priorities

### Backlog refinement

- Backlog reviewed weekly during sprint planning
- New issues added throughout sprint as discovered
- Issues NOT in current sprint go to backlog, prioritized for next sprint

---

## Sprint Ceremonies

### Sprint Planning (1 hour, Monday Week 1)

**Agenda**:
1. Review previous sprint outcomes (10 min)
2. Confirm sprint goal (10 min)
3. Assign tasks per role (30 min)
4. Identify dependencies and blockers (10 min)

**Outputs**:
- Sprint goal written in GitHub Projects
- All tasks assigned with owners
- Dependencies documented

### Daily Standup (15 min, Mon–Fri 09:00)

**Format** (each member, 2-3 min):
1. What I did yesterday
2. What I'll do today
3. Any blockers?

**Rules**:
- Cameras on (default)
- No deep-dive discussions — table for after standup
- Late = miss the meeting (no waiting)

### Sprint Review / Demo (1 hour, Friday Week 2)

**Agenda**:
1. Demo working features (40 min) — each role shows their work
2. Stakeholder feedback (15 min) — supervisor questions
3. Update on overall progress (5 min)

**Demo criteria**:
- Real working software, not slides
- Must run end-to-end (acceptable: with mocks where SAP/Azure not yet wired)
- Issues captured for next sprint

### Sprint Retrospective (30 min, after Review)

**Format** (rotate facilitator):
1. What went well? (10 min)
2. What could improve? (10 min)
3. Action items for next sprint (10 min)

**Output**: 1-3 action items, documented in `docs/retros/retro-sprint-N.md`

---

## Definition of Done

For a task to be considered **DONE** in any sprint:

- [ ] Code committed and pushed to repository
- [ ] Pull Request created with proper template
- [ ] At least 1 approval from another team member (CODEOWNERS where applicable)
- [ ] CI checks pass (build, tests, lint)
- [ ] PR merged to `develop`
- [ ] Issue closed with reference to PR
- [ ] Documentation updated if applicable
- [ ] Demo-ready in working state

For a **sprint** to be considered DONE:

- [ ] All committed tasks have status "Done"
- [ ] Sprint demo delivered to supervisor
- [ ] Retrospective conducted and action items recorded
- [ ] Sprint summary written in `docs/sprints/sprint-N-summary.md`

---

## Sprint Schedule

| Sprint | Dates | Theme | Major Milestone |
|---|---|---|---|
| Sprint 1 | May 13 – May 26 | Foundation | Echo bot working end-to-end |
| Sprint 2 | May 27 – Jun 9 | Core Integrations | Mock SAP + Azure OpenAI function calling |
| Sprint 3 | Jun 10 – Jun 23 | Feature Build I | SO queries + KPI dashboards |
| Sprint 4 | Jun 24 – Jul 7 | Feature Build II | All 15+ functions working |
| Sprint 5 | Jul 8 – Jul 21 | Workflow Actions | Release, reject, forward, substitution |
| Sprint 6 | Jul 22 – Aug 4 | Scheduled & Alerts | Hangfire jobs, alerts firing |
| Sprint 7 | Aug 5 – Aug 18 | PDF & Distribution | PDF generation + email via Graph |
| Sprint 8 | Aug 19 – Sep 1 | AI Bonus & Polish | Insights generation, edge cases |
| Sprint 9 | Sep 2 – Sep 15 | Docs & Rehearsal | All docs finalized, demo rehearsal |
| Sprint 10 | Sep 16 – Sep 30 | Defense Prep | Final polish, defense delivery |

**Mid-project review**: Recommend supervisor checkpoint after Sprint 5 (Jul 21).

---

## Sprint Details

### Sprint 1 — Foundation

**Dates**: May 13 – May 26, 2026
**Sprint Goal**: Establish development infrastructure and verify end-to-end bot communication.

#### Sprint deliverables by role

**BE Lead (Long)**
- [ ] Setup .NET solution with 8 projects
- [ ] Configure Docker Compose (PostgreSQL + Redis)
- [ ] Bot Framework SDK integration
- [ ] Echo bot working in Bot Framework Emulator
- [ ] Azure AD app registration
- [ ] Azure Bot Service resource created
- [ ] Connect Azure AD credentials to bot
- [ ] ngrok / Dev Tunnels setup for public endpoint
- [ ] Test bot sideload in Microsoft Teams

**SAP Team (2 members)**
- [ ] Install Eclipse ADT on both machines
- [ ] Connect to S40 system (UCC TUM)
- [ ] Explore standard SD tables (VBAK, VBAP, KNA1, etc.)
- [ ] Run sample transactions (VA01, VA03, VA05)
- [ ] Setup abapGit, link to repo `sap/abap/` subfolder
- [ ] Create Z-package for AISO objects
- [ ] Plan first Z-tables (design only)

**AI Team (2 members)**
- [ ] Apply for Azure OpenAI access
- [ ] Practice function calling in OpenAI Playground
- [ ] Review Microsoft Teams Toolkit (FE work)
- [ ] Design first 3 Adaptive Card mockups (welcome, help, error)

#### Sprint 1 Demo criteria
- ✓ Echo bot replies in Microsoft Teams (real, not emulator)
- ✓ SAP team can run VA03 in S40 and screenshot
- ✓ Azure OpenAI access request submitted
- ✓ GitHub repo structure complete with all required folders

#### Dependencies / risks
- 🚨 Microsoft Teams sideload requires M365 tenant admin consent — use Developer Program tenant
- 🚨 Azure OpenAI approval can take 1-3 days
- 🚨 SAP UCC system access depends on credentials being active

---

### Sprint 2 — Core Integrations

**Dates**: May 27 – Jun 9, 2026
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
- [ ] First 5 Adaptive Card templates designed (welcome, help, error, so-list, so-detail)
- [ ] Teams app manifest v1 + icons

#### Sprint 2 Demo criteria
- ✓ User types in Teams: "show recent orders" → bot returns formatted card with mock data
- ✓ Function call sequence visible in Application Insights
- ✓ SAP team shows VBAK data via CDS view in ADT
- ✓ Teams app installable with proper icon

#### Dependencies / risks
- 🚨 Mock SapClient must be ready before AI team can test function calling
- 🚨 Adaptive Card schema validation needed early to avoid rework

---

### Sprint 3 — Feature Build I

**Dates**: Jun 10 – Jun 23, 2026
**Sprint Goal**: Implement SO query and KPI features with real SAP data flowing for at least one path.

#### Sprint deliverables by role

**BE Lead**
- [ ] Real SapClient implementation (HttpClient + Polly retry + CSRF)
- [ ] OData query builder
- [ ] Switch one function from Mock to Real SAP path
- [ ] Bot dialog flow refactoring (state management, turn handlers)
- [ ] SSO OAuthPrompt integration
- [ ] Token caching

**SAP Team**
- [ ] First Analytical CDS View: `ZR_AISO_REVENUE_CUBE`
- [ ] First AMDP procedure: `ZCL_AISO_AMDP_REVENUE`
- [ ] First RAP Service Definition: `ZSD_AISO_SALES_ORDER`
- [ ] First RAP Service Binding: `ZSB_AISO_SALES_ORDER_V4`
- [ ] OData service activated and testable in /sap/opu/odata4/...
- [ ] Service consumable from outside SAP (basic auth)

**AI Team**
- [ ] Function definitions expanded to 10 functions
- [ ] System prompt v2 (Vietnamese + English support)
- [ ] Vietnamese test cases: 20+ queries
- [ ] Adaptive Cards expanded: 8-10 templates
- [ ] First KPI card with chart (revenue dashboard)
- [ ] Card rendering tested on Teams desktop + mobile

#### Sprint 3 Demo criteria
- ✓ User queries "show SO 5000123" → bot returns REAL data from SAP
- ✓ Revenue dashboard renders with chart for at least one mock customer
- ✓ Vietnamese query "hiển thị đơn hàng gần đây" works
- ✓ SSO sign-in flow functional in Teams

#### Dependencies / risks
- 🚨 SAP RAP service must be live for BE to connect
- 🚨 Cloud Connector status — if delayed, document direct connection workaround
- 🚨 Adaptive Card with charts more complex than text — may take longer

---

### Sprint 4 — Feature Build II

**Dates**: Jun 24 – Jul 7, 2026
**Sprint Goal**: Complete all data-query features (15+ functions) with both English and Vietnamese support.

#### Sprint deliverables by role

**BE Lead**
- [ ] All 15+ function handlers wired to AI Orchestrator
- [ ] Conversation state management with context retention
- [ ] Multi-turn conversation handling (follow-up references)
- [ ] Error handling refinement (graceful degradation)
- [ ] Redis caching layer for aggregation queries

**SAP Team**
- [ ] CDS views for: SO Aging, Delivery KPI, AR Aging
- [ ] AMDP procedures for: Aging buckets, Delivery rate
- [ ] OData services exposed for all KPI cubes
- [ ] Service performance testing (response time benchmarks)
- [ ] Documentation: /docs/sap-services.md

**AI Team**
- [ ] All 15+ function definitions complete
- [ ] System prompt v3 (refined based on testing)
- [ ] Test case suite: 50+ Vietnamese, 30+ English
- [ ] Edge case handling: ambiguous queries, out-of-scope
- [ ] All data cards complete: SO list, SO detail, customer history, all KPIs

#### Sprint 4 Demo criteria
- ✓ All 6 KPI cards render with real SAP data
- ✓ Vietnamese accuracy >85% on test suite
- ✓ Multi-turn conversation: "show orders" → "more details on first one" works
- ✓ Cards render correctly on mobile

#### Dependencies / risks
- 🚨 Test case suite needed for measurable AI quality
- 🚨 Performance: SAP query response time may need AMDP optimization

---

### Sprint 5 — Workflow Actions

**Dates**: Jul 8 – Jul 21, 2026
**Sprint Goal**: Implement workflow actions (release, reject, forward, substitution).

#### Sprint deliverables by role

**BE Lead**
- [ ] Workflow function handlers: release, reject, forward, substitution
- [ ] Attachment management (view, upload, download)
- [ ] Audit log entries for every action
- [ ] Authorization check before action execution

**SAP Team**
- [ ] RAP Behavior Definitions for SO actions (release, reject)
- [ ] RAP Behavior Implementations (ZBI_AISO_SO_HANDLER)
- [ ] PFCG roles: ZROLE_AISO_BOT_VIEWER, ZROLE_AISO_BOT_RELEASER
- [ ] Authorization objects setup
- [ ] Bot service user (ZBOT_USER) configured
- [ ] Substitution management via SAP standard or custom

**AI Team**
- [ ] Action confirmation prompts (require explicit user confirm)
- [ ] Reason code selection cards (rejection)
- [ ] User search via Microsoft Graph (forwarding)
- [ ] Date picker cards (substitution)
- [ ] Voice/tone for action confirmation cards

#### Sprint 5 Demo criteria
- ✓ User releases SO with comment, action reflected in SAP
- ✓ User rejects SO with reason code from dropdown
- ✓ Forward to colleague: M365 user found, mapped to SAP user, task assigned
- ✓ Substitution setup: rule created in SAP, expires after date range
- ✓ Audit log shows all actions with timestamps

#### Dependencies / risks
- 🚨 PFCG authorization is complex — allow time for trial-and-error
- 🚨 Microsoft Graph user search requires extra permissions consent
- 🚨 Action irreversibility: needs solid confirmation flow

#### Mid-project review checkpoint
At end of Sprint 5, conduct extended review with supervisor:
- Review all 4 main requirements coverage
- Assess timeline for remaining 5 sprints
- Adjust scope if needed (cut AI Bonus stretch goals if behind)

---

### Sprint 6 — Scheduled Reports & Alerts

**Dates**: Jul 22 – Aug 4, 2026
**Sprint Goal**: Automated background processes — weekly reports posted to channels, threshold-based alerts.

#### Sprint deliverables by role

**BE Lead**
- [ ] AISO.Scheduling: Hangfire setup with PostgreSQL storage
- [ ] Weekly report job (Monday 9 AM Vietnam time)
- [ ] Daily alert check job (revenue drop, delivery overdue)
- [ ] Channel subscription management (CRUD)
- [ ] Alert threshold configuration (per user, per channel)
- [ ] Hangfire dashboard accessible (admin)

**SAP Team**
- [ ] Background job for KPI snapshot refresh
- [ ] Cleanup job for audit log archiving
- [ ] Performance tuning: identified slow queries
- [ ] First end-to-end test via Cloud Connector (if available)

**AI Team**
- [ ] Weekly report card template (top customers, revenue trend, status)
- [ ] Alert notification cards (revenue drop, delivery overdue)
- [ ] Subscription management UI cards
- [ ] Alert configuration UI cards
- [ ] Multi-language for scheduled reports

#### Sprint 6 Demo criteria
- ✓ Weekly report appears in subscribed Teams channel automatically
- ✓ Manually trigger alert: card delivered with proper formatting
- ✓ User can subscribe/unsubscribe via bot command
- ✓ Threshold configuration: user sets target, system respects it

#### Dependencies / risks
- 🚨 Hangfire scheduler must trigger reliably
- 🚨 Microsoft Graph permissions for channel posting (`ChannelMessage.Send`)
- 🚨 Alert deduplication logic — don't spam users

---

### Sprint 7 — PDF & Distribution

**Dates**: Aug 5 – Aug 18, 2026
**Sprint Goal**: PDF generation and email distribution via Microsoft Graph.

#### Sprint deliverables by role

**BE Lead**
- [ ] AISO.Reporting: QuestPDF integration
- [ ] PDF template for monthly consolidated report
- [ ] Microsoft Graph email send integration
- [ ] Monthly report scheduled job (1st of month 8 AM)
- [ ] PDF export from any data card (on-demand)
- [ ] Excel/CSV export option for SO lists

**SAP Team**
- [ ] CDS views for monthly aggregation
- [ ] Performance optimization (caching strategy refined)
- [ ] Documentation: SAP service catalog
- [ ] Bug fixes from integration testing

**AI Team**
- [ ] Email body templates (Vietnamese + English)
- [ ] Email subject line generation
- [ ] PDF export action button on cards
- [ ] Recipient management UI cards
- [ ] Email preview before send

#### Sprint 7 Demo criteria
- ✓ Monthly PDF generated with charts, tables, branding
- ✓ Email sent via Microsoft Graph from team's M365 account
- ✓ PDF attachment delivered to recipient inbox
- ✓ On-demand PDF export works from any data card
- ✓ Excel export from SO list works

#### Dependencies / risks
- 🚨 QuestPDF learning curve
- 🚨 Microsoft Graph `Mail.Send` permission and consent
- 🚨 PDF rendering consistency (fonts, layouts)

---

### Sprint 8 — AI Bonus & Polish

**Dates**: Aug 19 – Sep 1, 2026
**Sprint Goal**: AI Bonus features (insights generation) and overall product polish.

#### Sprint deliverables by role

**BE Lead**
- [ ] Insights generation pipeline (data aggregation → LLM → output)
- [ ] Pseudonymization layer (replace customer names with IDs)
- [ ] Hallucination detection (verify numbers match real data)
- [ ] Application Insights integration (telemetry, custom events)
- [ ] Azure Key Vault integration (secrets management)
- [ ] Performance optimization based on metrics

**SAP Team**
- [ ] Aggregation views for insights context
- [ ] Anomaly detection helper procedures
- [ ] Final SAP performance tuning
- [ ] All SAP code reviewed and refactored
- [ ] SAP documentation finalized

**AI Team**
- [ ] Insights prompt design (analytical, recommendation-style)
- [ ] Insights card template
- [ ] Disclosure annotations (AI-generated)
- [ ] Comprehensive test suite re-run after refinements
- [ ] Edge case handling (low-data scenarios, contradictions)
- [ ] User feedback collection mechanism

#### Sprint 8 Demo criteria
- ✓ User asks "analyze this quarter" → bot returns insightful analysis
- ✓ Insights factually grounded (no hallucinations)
- ✓ Pseudonymization verified (customer names not sent to LLM)
- ✓ All features working together in integrated demo
- ✓ Application Insights showing system health

#### Dependencies / risks
- 🚨 LLM hallucination is real risk — needs strong validation
- 🚨 AI Bonus is "nice to have" — cut if timeline tight

---

### Sprint 9 — Documentation & Rehearsal

**Dates**: Sep 2 – Sep 15, 2026
**Sprint Goal**: All documentation finalized, demo rehearsed.

#### Sprint deliverables by role

**BE Lead**
- [ ] Technical Specification finalized
- [ ] Configuration Note completed
- [ ] Deployment guide written
- [ ] Architecture diagrams polished
- [ ] Code review and cleanup

**SAP Team**
- [ ] SAP-specific documentation (Z-objects catalog, auth model)
- [ ] Performance benchmarks documented
- [ ] Test scenarios and results
- [ ] Bug fix log

**AI Team**
- [ ] User Guide finalized (with all screenshots captured)
- [ ] AI testing report (accuracy metrics, examples)
- [ ] Adaptive Card library documented
- [ ] Demo script written
- [ ] Demo video recorded (backup)

**All members**
- [ ] Final Report (collaborative writing)
- [ ] Defense presentation slides
- [ ] Demo rehearsal (3 full runs with supervisor)
- [ ] FAQ document for likely defense questions

#### Sprint 9 Demo criteria
- ✓ Full demo runs end-to-end without intervention
- ✓ Demo script timed to 30 minutes
- ✓ All required documents submitted to school portal
- ✓ Backup demo video recorded

#### Dependencies / risks
- 🚨 Documentation often underestimated — start early
- 🚨 Demo rehearsal reveals integration issues — buffer for fixes

---

### Sprint 10 — Defense Preparation

**Dates**: Sep 16 – Sep 30, 2026
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
- [ ] Final bug fixes (only critical bugs)
- [ ] Production-like environment for defense demo
- [ ] Backup demo recorded in multiple quality levels

**SAP Team**
- [ ] SAP system warm-up scripts ready for demo
- [ ] Test data refresh before defense
- [ ] Backup SAP query examples ready

**AI Team**
- [ ] Demo conversation script with planned queries
- [ ] Backup queries if Azure OpenAI rate-limited
- [ ] Defense-day card library locked

#### Sprint 10 Demo criteria
- ✓ Defense delivered successfully
- ✓ All questions handled professionally
- ✓ Pass academic evaluation

#### Critical reminders
- 🔴 NO new features in Sprint 10 — only fixes and polish
- 🔴 Have offline backup of demo (recorded video)
- 🔴 Network outage plan for defense day

---

## Velocity Tracking

### Per-sprint velocity log

Track tasks completed per sprint to calibrate estimates.

| Sprint | Planned | Completed | Velocity |
|---|---|---|---|
| Sprint 1 | [TBD] | [TBD] | [TBD] |
| Sprint 2 | | | |
| Sprint 3 | | | |
| Sprint 4 | | | |
| Sprint 5 | | | |
| Sprint 6 | | | |
| Sprint 7 | | | |
| Sprint 8 | | | |
| Sprint 9 | | | |
| Sprint 10 | | | |

### Capacity calculation

- Team size: 5
- Hours per week per member: 20 (full-time student)
- Total team hours per sprint: 5 × 20 × 2 = **200 hours/sprint**
- Account for ceremonies (~10 hours/sprint): **190 net hours/sprint**

### Estimation guide (T-shirt sizes)
- **S** (Small): 2-4 hours
- **M** (Medium): 8-16 hours (1-2 days)
- **L** (Large): 16-32 hours (2-4 days)
- **XL** (Extra Large): 32+ hours (>4 days, consider splitting)

---

## Risk Management

### Critical risks tracked sprint-over-sprint

| Risk | Probability | Impact | Mitigation | Status |
|---|---|---|---|---|
| SAP Cloud Connector blocked | High | High | Mock-first dev, document fallback | Open |
| Azure OpenAI quota | Medium | Medium | Use credits wisely, have Gemini backup | Open |
| Team member dropout | Low | High | Cross-training, knowledge sharing | Mitigated |
| S40 system outage | Low | High | Pre-cache test data, recorded demo | Open |
| Vietnamese accuracy below 90% | Medium | Medium | Iterate prompts in Sprint 3-4 | Open |
| Authorization complexity | Medium | Medium | Simplify in Sprint 5, document target | Open |

### Risk review cadence
- Risk register reviewed every sprint retrospective
- Critical risks (Probability High + Impact High) reviewed weekly
- Escalation: BE Lead → Supervisor for risks blocking project

### Contingency budget

Reserve **20% of each sprint** for:
- Unplanned bugs
- Integration issues
- Re-work from sprint review feedback

---

## Appendix: Sprint Templates

### Sprint Planning Template

```markdown
# Sprint N Planning — [Sprint Theme]

**Date**: [Date]
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
```

### Sprint Summary Template

```markdown
# Sprint N Summary

**Period**: [Dates]
**Theme**: [Theme]

## Achievements
- [Major deliverables]

## Demo highlights
- [What was demonstrated]

## Metrics
- Tasks completed: X/Y
- Bugs identified: X
- PRs merged: X

## Carryover to next sprint
- [Items]

## Lessons learned
- [Key takeaways]
```

---

**Document version**: 1.0
**Last updated**: 2026-06-01
**Maintained by**: BE Lead (Trần Ngọc Quý Long)
