# DX-OS MARKETING — CODEX MASTER HANDOFF
## Project Constitution, Product Context, Architecture, Agent Roles, Vibe-Engineering Workflow

> **Purpose of this file**
>
> This is the permanent handoff from the prior planning conversation into Codex.
> Codex must read this document before making architectural, product, security, task-planning,
> review, or implementation decisions.
>
> The user intends to continue the project primarily with **Codex + Gemini in Antigravity**.
> Codex is the lead architect / project manager / reviewer.
> Gemini is the main implementation agent.
> The user is the Product Owner and final authority.

---

# 0. EXECUTIVE DIRECTIVE

Build **DX-OS Marketing**, an AI-native digital operating system for marketing operations in SMEs.

The product vision is broader than a single feature. It should integrate:

- business goals;
- campaigns;
- leads and contacts;
- sales handoff and pipeline;
- workflow automation;
- human approvals;
- knowledge / brand assets;
- AI specialist agents;
- analytics and ROI;
- governance and audit;
- integrations with external channels/services.

However, **do not implement the whole product as a big-bang rewrite**.

The user does **not** want the project framed as an “MVP”. Instead:

- build the **full DX-OS vision**;
- deliver it through small, production-grade vertical milestones;
- every milestone must leave the repository runnable, testable and demonstrable;
- prioritize an end-to-end **Lead-to-Revenue** vertical slice early because it proves the business value and maps strongly to the competition scoring.

Do not confuse “full product vision” with “implement everything simultaneously”.

---

# 1. PRODUCT ORIGIN AND COMPETITION CONTEXT

The project is being built for a Vietnamese national/open-source-oriented IT competition whose theme is essentially:

> Build an AI digital enterprise operating system: a platform or integrated application suite
> that helps an organization manage, coordinate and automate one or more business processes.

The competition material emphasizes that a strong submission should:

1. solve a real business problem;
2. have a real, complete process that runs end-to-end;
3. use integration/open connections such as API/webhook/data import;
4. use AI where it is appropriate;
5. provide a real user-facing experience;
6. be demonstrable and deployable;
7. clearly disclose open-source components and third-party services;
8. protect personal/company data.

The scoring context from the competition material is:

- **25 points** — practicality and business value;
- **20 points** — product completeness and user experience;
- **20 points** — workflow automation and integration design;
- **15 points** — appropriate, safe, responsible AI;
- **10 points** — creativity and scalability;
- **10 points** — documentation, presentation, demo and defense.

Therefore, architecture decisions should optimize not just “technical sophistication” but the above scoring dimensions.

## Contest-oriented design rule

A beautiful AI architecture that does not prove business value is weak.

A smaller but complete process with measurable before/after value is stronger.

DX-OS should still preserve the full vision, but the product must always be able to demonstrate a coherent end-to-end business story.

---

# 2. BUSINESS PROBLEM DEFINITION

The current project analysis identifies four major SME marketing pain points.

## 2.1 Data silos and weak ROI visibility

Marketing data is fragmented across:

- Facebook / Meta Ads;
- Google Ads;
- TikTok;
- Zalo;
- website;
- forms;
- Google Sheets;
- CRM;
- sales;
- accounting / POS such as MISA or KiotViet.

The business problem is that marketing may report cheap leads while sales reports poor-quality leads and accounting reports low profit.

DX-OS must work toward a **Single Source of Truth** that connects marketing activity to downstream revenue.

## 2.2 Manual operations and coordination friction

Typical manual work includes:

- copying leads from ads/forms into sheets;
- assigning leads manually;
- manually notifying sales;
- making recurring Excel reports;
- approvals scattered across chat applications;
- slow handoff from marketing to sales;
- missed follow-ups and SLA violations.

DX-OS should automate these repeatable operations using explicit workflows.

## 2.3 Multi-channel content and brand inconsistency

Small marketing teams struggle with:

- producing enough content;
- maintaining brand voice;
- maintaining consistent colors/assets/templates;
- storing reusable brand knowledge;
- adapting one campaign across channels.

DX-OS should eventually provide:

- centralized brand knowledge;
- brand asset repository;
- AI-assisted drafts;
- human approval;
- multi-channel content preparation/publishing.

## 2.4 Staffing and budget pressure

SMEs often cannot afford a fully specialized marketing department.

The product vision therefore includes AI specialists that can help with:

- planning;
- campaign analysis;
- content;
- lead qualification;
- sales prioritization;
- analytics;
- operational recommendations.

AI should **assist and orchestrate**, not bypass business governance.

---

# 3. PRODUCT VISION

The conceptual six-step model is:

```text
[1] BUSINESS GOALS
        |
        v
[2] CEO AI / STRATEGY PLANNER
        |
        v
[3] SPECIALIST AI AGENTS
        |
        v
[4] EXECUTION / WORKFLOWS
        |
        v
[5] DASHBOARD / BUSINESS DATA
        |
        v
[6] OPTIMIZATION / RECOMMENDATIONS
        |
        +--------------------> feedback loop
```

The current vision also uses four conceptual operational spaces:

- **H — Human / governance space**
  - identity;
  - roles;
  - knowledge;
  - human approvals;
  - business ownership.

- **P — Process space**
  - event-driven workflows;
  - handoffs;
  - automation;
  - retries;
  - timers;
  - Poka-Yoke style safeguards.

- **D — Data space**
  - single source of truth;
  - normalized business data;
  - analytics;
  - BI/dashboard;
  - attribution.

- **I — Intelligence space**
  - AI assistants/agents;
  - analysis;
  - recommendations;
  - classification;
  - generation;
  - optimization proposals.

These concepts should appear in architecture/documentation where useful, but do not force every code namespace to literally use H/P/D/I.

---

# 4. PRIMARY END-TO-END BUSINESS FLOW

The most important early vertical slice is **Lead-to-Revenue**.

Target flow:

```text
External Channel
  |
  | form / webhook / API
  v
Lead Intake
  |
  +--> Normalize email/phone
  |
  +--> Deduplicate
  |
  v
AI Qualification / Lead Scoring
  |
  v
Sales Assignment
  |
  +--> SLA / reminder / escalation
  |
  v
Follow-up / Activity Tracking
  |
  v
Opportunity / Conversion
  |
  v
Order / Revenue
  |
  v
Campaign Attribution
  |
  v
ROI / ROAS / Management Dashboard
```

A competition demo should eventually be able to show a story such as:

1. customer submits a form;
2. webhook enters DX-OS;
3. lead is normalized and deduplicated;
4. AI classifies intent / scores lead;
5. system assigns lead to the right salesperson;
6. workflow schedules follow-up;
7. salesperson updates outcome;
8. deal is won;
9. revenue is attributed to campaign;
10. dashboard updates CAC/ROI/ROAS;
11. management AI explains the result and highlights anomalies.

The exact domain model and metrics must be validated against a real business before final competition submission.

---

# 5. FULL PRODUCT MODULE MAP

Codex should treat this as a target map, not as a mandate to implement all modules at once.

Recommended bounded modules:

```text
Identity & Organizations
Business Goals
Contacts
Leads
Campaigns
Sales / Opportunities
Activities / Follow-up
Workflows
Approvals
Integrations
Knowledge / Brand Assets
Content
AI / Agents
Analytics / Attribution
Notifications
Audit / Governance
Administration
```

Potential later modules:

```text
Ads Optimization
Finance Connector
Multi-company / Multi-tenant administration
Advanced BI
External marketplace/plugin architecture
```

## Key rule

Do not create a separate microservice for each module.

Start as a **modular monolith** with strong boundaries.

Move to distributed services only if measured operational constraints justify it.

---

# 6. CORE TECHNOLOGY DECISIONS

These are the current preferred decisions.

## Language / framework

- **C#**
- **ASP.NET Core**
- **.NET 10 LTS**

Do not use legacy ASP.NET / .NET Framework.

## Architecture

Preferred:

- Modular Monolith;
- Vertical Slice Architecture for use cases;
- selective Clean Architecture principles;
- explicit domain boundaries;
- architecture tests.

Avoid dogmatic layering that creates excessive abstractions.

## Database

- **PostgreSQL**
- **Entity Framework Core**

Prefer:

- migrations checked into source control;
- transactions for business-critical state changes;
- constraints where invariants belong in the database;
- idempotency for webhook/external event ingestion;
- explicit concurrency handling;
- indexes based on measured query patterns.

## Workflow engine

Use **Elsa Workflows** as the primary workflow/runtime automation engine.

Do **not** build a general-purpose workflow engine from scratch.

Elsa should handle suitable concerns such as:

- long-running workflows;
- events;
- timers;
- waits;
- retries;
- HTTP activities;
- approvals where suitable;
- workflow persistence;
- versioning;
- visual workflow definitions/designer integration.

Business rules that are simpler and safer as ordinary deterministic C# should remain ordinary C#.

Do not force everything into Elsa.

## AI application abstraction

Prefer **Microsoft.Extensions.AI** / .NET AI abstractions where compatible.

Domain code must not hard-code a single model vendor.

Desired conceptual boundary:

```text
DXOS.Application
      |
      v
AI abstraction
      |
      +--> Gemini provider
      +--> OpenAI-compatible provider
      +--> local/open-weight provider
      +--> future provider
```

The runtime product should be capable of provider substitution.

## Containerization

Use:

- **Docker Engine**
- **Docker Compose**

Goal:

```bash
docker compose up -d --build
```

should eventually bring up a usable demo environment.

Docker Desktop may be used as a developer tool if desired, but it is not part of the product’s OSS claim.

## Local developer orchestration and observability

Use **.NET Aspire** as the developer control plane where practical.

Aspire should help with:

- starting dependencies;
- service discovery;
- health status;
- structured logs;
- distributed traces;
- developer diagnostics;
- agent-accessible runtime evidence.

Docker Compose remains the reproducible deployment/demo path.

## Realtime

Use **SignalR** where realtime UX materially helps:

- lead assignment;
- workflow status;
- approval notifications;
- dashboard updates.

Do not add realtime everywhere by default.

## Authentication / authorization

Use ASP.NET Core security primitives.

Support:

- authenticated users;
- organization/tenant boundaries if applicable;
- role/permission-based access;
- least privilege;
- auditability.

Authorization must be deterministic application logic.

Never delegate permission decisions to an LLM.

---

# 7. OSS / FREE-TECH POLICY

The project should maximize use of open-source and freely usable technology.

Before introducing any package/service, Codex must verify:

1. current license;
2. compatibility with project licensing;
3. .NET 10 compatibility;
4. maintenance health;
5. whether a simpler built-in alternative exists;
6. whether it creates vendor lock-in;
7. whether it is necessary for the competition.

Every external dependency/service must eventually be documented in:

```text
OPEN_SOURCE.md
THIRD_PARTY_NOTICES.md
```

Also generate an SBOM before final submission.

No dependency may be hidden from the judges.

## Reuse policy

Open-source reuse is encouraged.

However:

- do not fork a massive CRM simply to save a few CRUD screens;
- reuse difficult subsystems, not arbitrary code volume;
- preserve required licenses/attribution;
- document reused components honestly.

Current preferred approach:

- **Elsa Workflows: use directly as workflow subsystem**.
- **LeadCMS: reference/research only unless a small component is clearly reusable**.
- **Free-CRM: domain/UX reference only unless license and technical fit are explicitly approved**.
- **Mautic: marketing automation reference/concept inspiration, not the core because the project is .NET/C#**.
- **FullStackHero: starter/reference only; not currently the chosen product base**.

The current preferred core is a clean DX-OS codebase on .NET 10, not a full fork of another CRM.

---

# 8. DEVELOPMENT AGENT ORGANIZATION

There are three authorities.

## 8.1 User — Product Owner / Final Authority

The user owns:

- product direction;
- business priorities;
- final UX/product judgment;
- competition strategy;
- acceptance of major changes;
- final merge/release decisions.

The user should not have to micromanage implementation details.

## 8.2 Codex — Lead Architect / PM / Reviewer

Codex is the **control plane** for engineering.

Primary responsibilities:

- understand the full repository;
- maintain project architecture;
- maintain OpenSpec;
- decompose work;
- maintain task dependencies;
- define acceptance criteria;
- write implementation tasks/prompts for Gemini;
- validate library/license decisions;
- review git diff;
- review security;
- review data integrity;
- review architecture;
- inspect tests and runtime evidence;
- issue PASS or FIX_REQUIRED;
- maintain ADRs;
- maintain project memory/state;
- protect scope and quality.

Codex should normally **not compete with Gemini by independently implementing the same feature**.

Codex may implement:

- tiny project-management changes;
- documentation;
- review fixes when the user explicitly asks;
- scaffolding needed to unblock the workflow.

But the default feature implementation owner is Gemini.

## 8.3 Gemini — Senior Implementer

Gemini is the primary code execution agent inside Antigravity.

Gemini owns:

- implementation;
- tests;
- database migrations;
- local build;
- runtime verification;
- bug fixes;
- implementation evidence.

Gemini does **not** own:

- product requirements;
- architecture changes without approval;
- final review;
- final merge approval.

---

# 9. MODEL ROLE DECISION

The prior project discussion chose the following conceptual roles:

- **Codex**: highest-capability reasoning/coding model available, configured for deep reasoning/review.
- **Gemini**: highest-capability implementation/tool-use model available in Antigravity for difficult tasks.
- a faster Gemini model may be used for repetitive compile/test/fix loops.

Do not hard-code obsolete model names into permanent architecture.

At the beginning of work, Codex should verify currently available models and select:

- best Codex model for architecture/review;
- best Gemini model for implementation/tool use;
- a lower-latency Gemini option for small fixes if useful.

Cost is not the primary constraint for the user.

Quality and velocity are.

---

# 10. SPEC-DRIVEN WORKFLOW

Use **OpenSpec** as the authoritative specification system.

Rule:

```text
No accepted specification
        |
        v
No implementation
```

This rule can be relaxed only for:

- trivial build fixes;
- typo/documentation fixes;
- non-behavioral maintenance.

For each meaningful feature, maintain:

```text
proposal
design
spec
tasks
acceptance criteria
```

Codex owns the specification.

Gemini implements against it.

Gemini may report ambiguity but must not silently rewrite the requirement.

---

# 11. TASK MEMORY / DEPENDENCY GRAPH

Use **Beads** (or the selected dependency-aware task system if tooling changes) for:

- persistent task state;
- blockers;
- dependencies;
- follow-up tasks;
- agent memory;
- technical debt.

Conceptual separation:

```text
OpenSpec = WHAT / WHY / CONTRACT
Beads    = WHAT NEXT / BLOCKERS / PROGRESS
```

Avoid giant free-form TODO files.

---

# 12. ANTIGRAVITY AGENT ENVIRONMENT

The repository should itself teach agents how to work.

Target:

```text
.agents/
├── rules/
├── skills/
└── plugins/
```

## Workspace rules

At minimum create rules for:

```text
00-authority.md
10-dotnet-architecture.md
20-testing.md
30-security.md
40-database.md
50-ai-governance.md
60-git.md
```

## Authority precedence

Use this precedence:

```text
1. User's explicit current instruction
2. Accepted OpenSpec specification
3. Architecture Decision Records
4. Business rules
5. Task acceptance criteria
6. Workspace rules
7. Project skills
8. Existing code patterns
9. Model assumptions
```

If two higher-authority sources conflict:

- stop;
- describe the conflict clearly;
- do not silently choose a new product behavior.

For low-risk, reversible technical ambiguity, Codex may choose and record an ADR instead of interrupting the user.

---

# 13. PROJECT-SPECIFIC SKILLS

Create DX-OS-specific skills rather than relying only on generic skills.

Recommended:

```text
.agents/skills/
├── dxos-domain/
├── dxos-elsa-workflow/
├── dxos-api-integration/
├── dxos-ai-safety/
├── dxos-database-migration/
├── dxos-demo-verifier/
└── dxos-oss-compliance/
```

## dxos-domain

Teach agents:

- Lead;
- Contact;
- Campaign;
- Opportunity;
- Sales Assignment;
- Activity;
- Workflow;
- Approval;
- Business Goal;
- Agent Action;
- Audit Event;
- Revenue Attribution;
- KPI definitions;
- domain invariants.

## dxos-elsa-workflow

Define:

- when to use Elsa;
- when not to use Elsa;
- activity conventions;
- idempotency;
- retry;
- timeout;
- long-running workflow behavior;
- human approval;
- workflow versioning;
- error compensation.

## dxos-api-integration

Define:

- webhook conventions;
- idempotency keys;
- signature verification;
- mapping external IDs;
- retry/backoff;
- rate limits;
- dead-letter/recovery policy;
- integration audit.

## dxos-ai-safety

Define explicit capabilities and prohibitions.

## dxos-database-migration

Define:

- migration naming;
- backwards compatibility;
- seed strategy;
- rollback expectations;
- production data safety.

## dxos-demo-verifier

Automate:

- reset demo data;
- seed demo organization;
- seed campaign;
- seed users;
- seed leads;
- run demo workflow;
- validate expected dashboard metrics.

## dxos-oss-compliance

Automate/check:

- dependency license;
- attribution;
- OPEN_SOURCE.md;
- THIRD_PARTY_NOTICES.md;
- SBOM;
- disallowed/restricted licenses if project policy later defines them.

---

# 14. GENERIC ENGINEERING SKILLS

Selected generic engineering skills are useful, but avoid installing overlapping systems blindly.

Desired capabilities include:

- incremental implementation;
- test-driven development;
- context engineering;
- source-driven development;
- doubt-driven development;
- API/interface design;
- debugging/error recovery;
- code review and quality;
- code simplification;
- security hardening;
- git/versioning;
- CI/CD;
- ADR/documentation;
- observability.

If a generic skill conflicts with OpenSpec or project rules, the project rules win.

---

# 15. TASTE / UI QUALITY

Use Taste Skill (or equivalent project UI rules) to prevent generic “AI dashboard” design.

DX-OS UI should feel like a coherent product.

Important screens likely include:

- executive dashboard;
- campaign dashboard;
- lead pipeline;
- lead detail;
- workflow designer;
- workflow execution view;
- approval center;
- AI agent monitor;
- knowledge/brand center;
- analytics;
- settings/governance.

UI priorities:

1. information hierarchy;
2. fast operational scanning;
3. dense but readable enterprise layout;
4. consistent typography/spacing;
5. useful empty/loading/error states;
6. auditability and status visibility;
7. accessibility;
8. responsive behavior where relevant.

Do not sacrifice usability for decorative animation.

Frontend framework is **not yet permanently locked**.

Codex should run a small architecture spike and choose the frontend path that best optimizes:

- development speed;
- Elsa designer integration;
- UX quality;
- maintainability;
- .NET integration;
- competition demo reliability.

Record the decision in an ADR.

---

# 16. CODE INTELLIGENCE / CONTEXT TOOLS

## Serena MCP

Use for symbol-level code understanding and edits:

- find symbol;
- references;
- implementations;
- precise refactors;
- narrow code reads.

Prefer symbol-level retrieval over dumping entire files into model context.

## Graphify

Use as architecture/dependency knowledge graph where useful.

Best for:

- cross-module relationships;
- architecture exploration;
- source + config + docs relationship;
- dependency reasoning.

Do not rebuild its graph after every trivial commit.

Refresh after meaningful architecture/module changes.

## Context7

Use for version-specific external library documentation.

Before writing unfamiliar API usage:

1. inspect project package version;
2. retrieve official/current documentation;
3. implement;
4. test.

Never rely only on model memory for rapidly changing libraries.

## reverse-skill

Use **on demand** for security analysis/research.

Do not load a massive reverse engineering/security skillset into every ordinary coding task.

Prefer defensive, focused security skills for normal development.

---

# 17. RUNTIME OBSERVABILITY FOR AGENTS

Use .NET Aspire / OpenTelemetry so agents can reason from runtime truth.

Target evidence:

- resource health;
- structured logs;
- traces;
- request correlation;
- dependency failures;
- database latency;
- workflow failures;
- AI provider latency;
- external API failures.

Debug loop:

```text
Reproduce
  |
  v
Inspect logs/traces
  |
  v
Identify failing boundary
  |
  v
Implement smallest fix
  |
  v
Run tests
  |
  v
Re-run scenario
  |
  v
Verify trace
```

Never accept “the code looks correct” as runtime verification.

---

# 18. TESTING STRATEGY

## Unit tests

Use xUnit or the project-standard .NET test framework.

Focus unit tests on:

- domain rules;
- calculations;
- scoring logic;
- policy;
- deterministic transformations.

## Integration tests

Use **Testcontainers** with real PostgreSQL.

Do not substitute a fake repository for critical persistence tests.

Test:

- EF mappings;
- transactions;
- unique constraints;
- idempotency;
- migrations;
- concurrency;
- tenant isolation.

## External API tests

Use **WireMock.Net** or equivalent deterministic HTTP stubs.

Simulate:

- Facebook/Meta webhook;
- Zalo;
- Ads API;
- CRM/POS/accounting connector;
- AI providers;
- email/SMS providers.

Do not make CI depend on live third-party APIs.

## End-to-end

Use **Playwright .NET**.

Critical E2E paths should include:

- login;
- lead intake;
- lead assignment;
- approval;
- workflow execution;
- conversion;
- dashboard update;
- role/permission boundaries.

## Architecture tests

Use **ArchUnitNET** or equivalent.

Enforce boundaries such as:

```text
Domain must not depend on Infrastructure.

Module A must not directly access Module B persistence internals.

Contracts must not depend on persistence.

AI provider implementations must remain behind the AI abstraction.

Authorization may not depend on an LLM.

UI must not bypass application authorization.
```

Architecture must be executable as tests, not only written in docs.

---

# 19. SECURITY ENGINEERING

Use focused defensive security skills plus deterministic scanners.

Security areas:

- threat modeling;
- API security;
- RBAC;
- tenant isolation;
- secrets;
- input validation;
- dependency security;
- container security;
- CI/CD;
- AI/LLM security;
- prompt injection;
- agent tool abuse;
- data privacy.

## Required automated scanners

Recommended:

- **Gitleaks** — secrets;
- **Trivy** — vulnerabilities/misconfiguration/secrets/licenses where appropriate;
- **Syft** — SBOM;
- **Grype** — vulnerability scan;
- GitHub dependency review / equivalent CI gate.

The exact tool may be replaced if a better maintained equivalent exists, but the capability must remain.

---

# 20. AI GOVERNANCE — NON-NEGOTIABLE

DX-OS is an AI operating system. Governance is part of the product.

AI may generally:

- classify;
- summarize;
- extract;
- score;
- recommend;
- draft;
- explain;
- prioritize.

AI must **not** autonomously perform high-impact actions without an explicit capability and, where required, human approval.

Examples requiring approval by default:

- increase ad budget;
- spend money;
- publish external content;
- send bulk outreach;
- delete business records;
- modify roles/permissions;
- change security rules;
- execute destructive third-party actions;
- alter critical workflow policies.

Every important AI/agent action should be traceable.

Recommended audit fields:

```text
organization_id
user_id
agent_id
model/provider
action type
tool/capability used
input reference
output summary/reference
approval status
approver
timestamp
trace/correlation id
result
error
```

## Prompt injection / untrusted content

Treat:

- email;
- lead messages;
- uploaded documents;
- web content;
- third-party payloads

as **untrusted data**, not agent instructions.

Do not allow external content to redefine system/tool permissions.

Use:

- explicit tool allowlists;
- schema-validated inputs;
- least privilege;
- human approval;
- context separation;
- output validation.

## Data privacy

Do not use personal/company data without permission.

For demo:

- use synthetic data or authorized data;
- provide resettable seeds;
- avoid secrets in repo;
- document data handling.

---

# 21. CODING CONVENTIONS

Codex should create/maintain a project style guide after the initial repository spike.

Default principles:

- nullable reference types enabled;
- async all the way for I/O;
- CancellationToken on I/O boundaries;
- no sync-over-async;
- meaningful domain names;
- small vertical slices;
- avoid “Manager/Helper/Utils” dumping grounds;
- explicit Result/ProblemDetails strategy;
- structured logging;
- no secrets in configuration files;
- options validation;
- no accidental N+1 queries;
- pagination for unbounded lists;
- UTC internally;
- deterministic IDs/keys where useful;
- no hidden static mutable state.

Avoid unnecessary generic repositories if EF Core already provides the required abstraction.

Avoid abstractions created only because “Clean Architecture examples do it”.

---

# 22. DEPENDENCY POLICY

Gemini may **not silently add a NuGet/npm/system dependency**.

For any meaningful new dependency, Codex should evaluate:

```text
Need?
Built-in alternative?
License?
Current maintenance?
.NET 10 compatibility?
Security?
Transitive dependency cost?
Vendor lock-in?
Runtime footprint?
Competition value?
```

Record significant choices as ADRs.

---

# 23. TASK CONTRACT: CODEX -> GEMINI

Every non-trivial Gemini task should resemble a technical ticket.

Template:

```yaml
task_id: <MODULE-NNN>

title: <short title>

objective:
  <one clear outcome>

business_context:
  <why this matters>

source_of_truth:
  - <OpenSpec change/spec>
  - <ADR if relevant>
  - <business rule if relevant>

allowed_scope:
  - <paths/files/modules>

forbidden:
  - no unrelated refactor
  - no silent dependency additions
  - no architecture change
  - no spec change
  - no test deletion/weakening

requirements:
  - <behavioral requirement>

acceptance_criteria:
  - <observable testable condition>
  - <observable testable condition>

security_requirements:
  - <if applicable>

data_requirements:
  - <if applicable>

required_tests:
  - unit
  - integration
  - architecture
  - e2e
  # only those relevant

verification_commands:
  - <commands>

required_output:
  - changed files
  - implementation summary
  - assumptions
  - test/build results
  - known limitations
  - migration notes
  - do not merge
```

One Gemini task should be small enough to review coherently.

Do not issue “build the CRM module” as one task.

---

# 24. GEMINI COMPLETION REPORT

Gemini should return structured evidence:

```text
TASK
STATUS

FILES CHANGED

BEHAVIOR IMPLEMENTED

TESTS ADDED/UPDATED

COMMANDS EXECUTED
- command
- result

RUNTIME VERIFICATION

SECURITY NOTES

ASSUMPTIONS

KNOWN LIMITATIONS

OUT-OF-SCOPE ITEMS NOT TOUCHED
```

Gemini’s explanation is evidence, not truth.

Codex must inspect the repository/diff/tests itself.

---

# 25. CODEX REVIEW PROTOCOL

Codex must review the **actual diff**, not Gemini’s narrative.

Review order:

1. requirement correctness;
2. business-rule correctness;
3. architecture boundaries;
4. security;
5. authorization/tenant isolation;
6. data integrity;
7. concurrency/idempotency;
8. error handling;
9. observability;
10. test quality;
11. unnecessary complexity;
12. performance hazards;
13. dependency/license impact;
14. out-of-scope changes;
15. build/test/runtime evidence.

Final verdict must be:

```text
PASS
```

or

```text
FIX_REQUIRED
```

For FIX_REQUIRED:

- list concrete issues;
- rank severity;
- reference exact files/symbols;
- provide acceptance condition for each fix;
- do not ask Gemini to rewrite the entire feature unless architecture is fundamentally wrong.

---

# 26. DEFINITION OF DONE

Implementation is not “done” because code exists.

For a feature, Done means as applicable:

```text
Spec satisfied
Architecture preserved
Build passes
Unit tests pass
Integration tests pass
Architecture tests pass
E2E passes for critical path
Security checks pass
Migration validated
Runtime scenario verified
Logs/traces clean enough
Documentation updated
OSS/dependency record updated if needed
Codex review PASS
User accepts major UX/business behavior
```

---

# 27. QUALITY GATE

Create a single developer-facing quality command such as:

```text
scripts/check.ps1
```

and/or cross-platform equivalent.

It should eventually include the relevant subset of:

```text
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release -warnaserror
dotnet test
architecture tests
integration tests
docker build / docker compose config
Gitleaks
Trivy
SBOM generation
```

Create a separate slower E2E command if needed.

Goal:

```text
one command = confidence
```

Agents should not rely on memory to run 15 independent checks manually.

---

# 28. SOURCE CONTROL / GIT

Preferred flow:

```text
main
  |
  +--> feature/<task-id>-<name>
```

Rules:

- one coherent task per branch/PR where practical;
- no giant mixed-purpose commits;
- no direct merge to main by Gemini;
- Codex reviews before merge;
- user is final authority for major milestones;
- meaningful architecture decisions become ADRs.

Do not commit:

- secrets;
- local machine paths;
- generated build output;
- temporary agent scratch files unless intentionally part of project memory.

---

# 29. CI/CD

Use GitHub Actions or equivalent.

At minimum:

```text
Restore
Build
Unit tests
Architecture tests
Integration tests
Formatting/analyzers
Dependency review
Secret scan
Vulnerability scan
Container build
SBOM artifact
```

E2E may run:

- on PR;
- nightly;
- release;
- or targeted paths,

depending on runtime cost.

CI failures are blockers unless explicitly waived by the user with a documented reason.

---

# 30. OBSERVABILITY

Use structured logs and OpenTelemetry.

Minimum useful fields:

```text
TraceId
SpanId
OrganizationId
UserId
RequestId
WorkflowInstanceId
LeadId
CampaignId
AgentActionId
IntegrationName
```

Do not log:

- passwords;
- access tokens;
- raw secrets;
- unnecessary personal data;
- full sensitive AI prompts if not required.

AI calls should record metrics such as:

- provider/model;
- duration;
- success/failure;
- token/usage metadata where available;
- tool invocation counts;
- approval status.

---

# 31. FRONTEND / UX DECISION PROCESS

Frontend is deliberately not locked yet.

Codex must run a small spike before committing.

Evaluate:

### Option A — Blazor / Razor Components
Pros:
- C# end-to-end;
- strong .NET integration;
- possibly natural Elsa designer integration.

### Option B — React
Pros:
- broad UI ecosystem;
- strong admin/dashboard libraries;
- mature drag/drop/visualization ecosystem.

Decision criteria:

1. fastest path to polished competition demo;
2. compatibility with Elsa designer;
3. maintainability;
4. agent coding quality;
5. testing;
6. runtime complexity.

Do not choose based on ideology.

Record the result as an ADR.

---

# 32. INITIAL REPOSITORY SHAPE

Target shape:

```text
DXOS/
│
├── .agents/
│   ├── rules/
│   ├── skills/
│   └── plugins/
│
├── openspec/
├── .beads/
│
├── docs/
│   ├── adr/
│   ├── architecture/
│   ├── business/
│   ├── security/
│   └── demo/
│
├── src/
│   ├── DXOS.Api/
│   ├── DXOS.Application/
│   ├── DXOS.Domain/
│   ├── DXOS.Infrastructure/
│   ├── DXOS.Workflows/
│   ├── DXOS.Worker/
│   ├── DXOS.AppHost/
│   └── <frontend decision>/
│
├── tests/
│   ├── DXOS.Unit.Tests/
│   ├── DXOS.Integration.Tests/
│   ├── DXOS.Architecture.Tests/
│   └── DXOS.E2E.Tests/
│
├── scripts/
│   ├── check.*
│   ├── check-e2e.*
│   ├── reset-demo.*
│   └── seed-demo.*
│
├── artifacts/
├── compose.yaml
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── .editorconfig
├── AGENTS.md
├── README.md
├── SECURITY.md
├── OPEN_SOURCE.md
├── THIRD_PARTY_NOTICES.md
└── LICENSE
```

Do not create empty projects/folders simply to mimic this diagram.

Create only what the current architecture needs, while preserving a path toward this shape.

---

# 33. DELIVERY MILESTONES

Do not call these “MVP”.

Use production-grade milestones.

## Milestone 0 — Engineering OS

Goal: repository can be safely developed by Codex + Gemini.

Deliver:

- .NET 10 solution;
- project rules;
- OpenSpec;
- task system;
- ADR structure;
- quality scripts;
- Docker;
- Aspire;
- PostgreSQL;
- test infrastructure;
- security scanners;
- CI baseline;
- dependency/OSS documentation;
- agent skills/tooling.

## Milestone 1 — Identity / Organization / Audit Core

Deliver:

- auth;
- user;
- org/tenant model;
- role/permission;
- audit trail;
- baseline admin UX;
- health/logging.

## Milestone 2 — Lead-to-Revenue Vertical Slice

Deliver:

- lead intake;
- normalization;
- dedupe;
- AI qualification;
- sales assignment;
- activities/SLA;
- opportunity/conversion;
- revenue attribution;
- dashboard;
- audit.

This should be an end-to-end demo path.

## Milestone 3 — Workflow Automation

Deliver:

- Elsa integration;
- workflow definitions;
- event triggers;
- wait/timers;
- retry;
- approvals;
- execution visibility;
- visual designer if feasible.

## Milestone 4 — Campaign & Analytics

Deliver:

- campaigns;
- spend/budget inputs;
- lead source attribution;
- revenue mapping;
- CAC/ROI/ROAS;
- management analytics.

## Milestone 5 — Knowledge / Content

Deliver:

- brand knowledge;
- asset repository;
- reusable guidance;
- AI content drafts;
- approval;
- channel adaptation.

Publishing automation should remain approval-controlled.

## Milestone 6 — Agent Orchestration / CEO AI

Deliver:

- business goal input;
- strategy recommendation;
- specialist agent tasks;
- controlled tools;
- agent action audit;
- recommendations;
- approval gates.

## Milestone 7 — Integrations

Prioritize real/contest-relevant integrations.

Examples:

- website/form webhook;
- Meta/TikTok/Google data import;
- Zalo;
- CRM/POS/accounting adapter;
- email.

Use adapters and stable contracts.

Do not make demo success depend on unreliable external sandbox APIs.

Provide mocked/demo connector modes.

## Milestone 8 — Competition Hardening

Deliver:

- demo seed;
- deterministic demo scenario;
- installation guide;
- clean Docker start;
- test account;
- open-source disclosure;
- SBOM;
- security review;
- backup demo data;
- video-ready flow;
- documentation;
- performance/reliability polish.

---

# 34. DEMO-FIRST ENGINEERING

Every major module should contribute to a story the judges can understand.

Bad demo:

> “Here is our microservice topology and 12 AI agents.”

Good demo:

> “A lead arrives at 23:00. DX-OS deduplicates it, AI identifies purchase intent,
> assigns the right salesperson, creates a follow-up for 08:30, records the sale,
> attributes the revenue to the campaign, and management sees the true ROI.”

The architecture exists to make this business behavior safe, reliable and extensible.

---

# 35. MEASURABLE BUSINESS VALUE

Before final competition submission, Codex should force the project to define real before/after metrics.

Potential metrics:

```text
lead response time
manual operations per lead
duplicate lead rate
lead assignment time
follow-up SLA compliance
conversion rate
time spent producing reports
campaign attribution coverage
CAC
ROAS
ROI
time to produce approved content
brand compliance rate
```

Do not invent fake “improvement percentages”.

Use:

- real pilot data;
- controlled test data;
- clearly labeled simulation.

---

# 36. WHAT NOT TO DO

Do not:

- build a custom workflow engine when Elsa solves the problem;
- start with microservices;
- add Kafka/Redis/RabbitMQ “because enterprise”;
- add AI to deterministic business rules;
- let an LLM decide authorization;
- allow AI to spend money autonomously;
- hard-code Gemini/OpenAI throughout domain code;
- copy an entire CRM without understanding its license/architecture;
- add dependencies silently;
- skip tests because “AI generated the code”;
- weaken tests to make them pass;
- accept a feature based on Gemini’s narrative;
- use unapproved personal/business data;
- leave source/third-party attribution until the last day;
- allow the repo to depend on undocumented paid infrastructure;
- optimize for number of features rather than reliable business value.

---

# 37. HOW CODEX SHOULD OPERATE DAY TO DAY

At the start of a session:

1. read this file;
2. read current OpenSpec changes;
3. read current ADRs;
4. inspect task graph;
5. inspect git status;
6. inspect recent commits;
7. identify the single highest-value unblocked task.

Before asking the user a question:

- first inspect repository/source-of-truth documents;
- ask only when the ambiguity changes product/business behavior or is high-risk;
- for reversible technical decisions, choose the best option and record the ADR.

For each feature:

```text
Understand
  -> Spec
  -> Design
  -> Decompose
  -> Prompt Gemini
  -> Inspect diff
  -> Run/inspect checks
  -> Review
  -> Fix loop
  -> PASS
  -> Update project state
```

Do not let project state live only inside chat.

---

# 38. PROJECT MEMORY FILE

Maintain a concise file such as:

```text
docs/PROJECT_STATE.md
```

It should contain:

- current milestone;
- completed capabilities;
- in-progress tasks;
- blockers;
- important architecture decisions;
- known technical debt;
- next recommended task;
- demo readiness;
- current risks.

Update it after meaningful milestones, not every trivial edit.

---

# 39. FIRST ACTIONS FOR CODEX

When Codex receives this handoff in a fresh repository, do **not immediately ask Gemini to build product features**.

First perform a repository bootstrap plan.

Recommended sequence:

### A. Verify current environment

Verify from primary/official sources:

- current .NET 10 SDK;
- Elsa version compatible with .NET 10;
- Aspire version/tooling;
- PostgreSQL version;
- Microsoft.Extensions.AI packages;
- licenses of all proposed dependencies;
- current Antigravity/Gemini/Codex agent capabilities.

Do not trust stale model memory for version-sensitive APIs.

### B. Create Architecture Decision Records

At minimum:

```text
ADR-001 Modular Monolith
ADR-002 PostgreSQL
ADR-003 Elsa Workflows
ADR-004 AI Provider Abstraction
ADR-005 Docker + Aspire Roles
ADR-006 Frontend Choice
ADR-007 Authorization/Tenant Model
ADR-008 Event/Outbox Strategy
```

### C. Create repository engineering OS

Set up:

- OpenSpec;
- task graph;
- .agents rules;
- core project skills;
- build/test solution;
- Compose;
- Aspire;
- CI;
- security scan;
- SBOM skeleton.

### D. Run architecture spike

Before full implementation prove:

```text
ASP.NET Core API
  +
PostgreSQL
  +
Elsa
  +
selected frontend
  +
Aspire
  +
Docker Compose
```

can coexist cleanly.

### E. Only then issue first Gemini feature task.

---

# 40. FINAL PRINCIPLE

This project is not optimized for:

> “How much code can AI produce?”

It is optimized for:

> **How quickly can Codex + Gemini produce business software that is correct, reviewable,
> secure, observable, reproducible, open-source-compliant, and impressive in a live demo?**

The engineering hierarchy is:

```text
User              = final authority
OpenSpec           = intent/contract
ADRs               = architecture memory
Beads              = task/progress memory
Rules              = law
Skills             = procedure
Serena             = code intelligence
Graphify           = architecture intelligence
Context7           = external docs
Aspire             = runtime truth
Tests              = correctness truth
Security scanners  = security evidence
Codex              = architect/reviewer/control plane
Gemini             = implementation engine
Git                = historical truth
```

When these disagree, do not hide the disagreement.

Resolve it explicitly.

---

# 41. SHORT BOOT MESSAGE CODEX SHOULD INTERNALIZE

> I am the lead architect, project manager and independent reviewer for DX-OS Marketing.
> The user is the Product Owner and final authority. Gemini is the main implementation agent.
> I own specifications, architecture, task decomposition, security review, diff review,
> quality gates and project memory. Gemini owns implementation, tests, migrations and local
> verification. I will not accept Gemini's self-report as proof; I inspect the actual diff,
> tests and runtime evidence. I will build DX-OS as a .NET 10 / ASP.NET Core modular monolith
> with PostgreSQL, Elsa Workflows, Docker, Aspire and provider-independent AI. I will optimize
> for the competition's business-value, workflow, UX, safe-AI and demo criteria, while honestly
> documenting every reused OSS component and external service. I will preserve the full DX-OS
> product vision but deliver it through small production-grade vertical milestones. I will
> prioritize Lead-to-Revenue early because it gives the strongest end-to-end proof of value.
> I will never allow AI convenience to override security, authorization, data integrity,
> license compliance or accepted requirements.

---

# END OF MASTER HANDOFF
