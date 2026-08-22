## Purpose

Defines how OpenSpec, Beads, agent authority, project state, and durable task evidence govern remediation without allowing chat narratives or implementer self-reports to become project truth.

## ADDED Requirements

### Requirement: OpenSpec is the remediation contract
The accepted `bootstrap-remediation-001` change SHALL be the authoritative WHAT, WHY, behavioral contract, design, research, plan, and task definition for bootstrap remediation.

#### Scenario: Implementation conflicts with the accepted change
- **WHEN** an implementation or agent proposal would change scope, requirements, architecture, or acceptance criteria
- **THEN** implementation stops until the OpenSpec change is updated and reviewed

### Requirement: Beads is the execution state graph
Beads SHALL track assignment, readiness, dependencies, blockers, and completion, while OpenSpec task entries SHALL reference the corresponding Beads issue without duplicating execution state.

#### Scenario: Task readiness is queried
- **WHEN** an agent selects the next remediation task
- **THEN** it reads Beads readiness/dependencies and the referenced OpenSpec contract
- **AND** does not infer readiness from checklist text alone

### Requirement: Task mapping is bidirectional
Every implementation task in `tasks.md` MUST identify its Beads issue, and every mapped Beads issue MUST identify the relevant OpenSpec task IDs and change path.

#### Scenario: Mapping consistency is validated
- **WHEN** the remediation package is reviewed
- **THEN** each implementation task resolves to one operational issue
- **AND** each remediation issue resolves to its authoritative task contract

### Requirement: Authority order is explicit
Repository agent instructions SHALL enforce this precedence: current user instruction, the DX-OS constitution, accepted OpenSpec, accepted ADRs, business rules, Beads acceptance criteria, workspace rules, project skills, existing code patterns, and model assumptions. The constitutional requirement that DX-OS itself is open source is non-negotiable and cannot be weakened by a lower-authority artifact.

#### Scenario: Higher-authority sources conflict
- **WHEN** two applicable higher-authority sources require incompatible behavior
- **THEN** the agent stops and reports the conflict without silently choosing a product behavior

### Requirement: Implementer and reviewer roles remain independent
Gemini SHALL act as the primary implementer for remediation tasks, while Codex SHALL independently inspect repository state, diff, commands, exit codes, tests, runtime evidence, security, and OSS state before issuing exactly `PASS` or `FIX_REQUIRED`.

#### Scenario: Gemini reports completion
- **WHEN** an implementation report is returned
- **THEN** Codex treats the report as evidence to verify rather than truth
- **AND** task completion is not recorded before independent review

### Requirement: Task evidence is durable and bounded
Each remediation task SHALL store engineering evidence under `artifacts/task-runs/<task-id>/` using the defined evidence filenames and MUST NOT store raw conversation history as project truth.

#### Scenario: Task prompt is issued
- **WHEN** Codex authorizes a Gemini implementation task
- **THEN** `prompt.md` exists before execution and contains the approved contract

#### Scenario: Task review completes
- **WHEN** Codex reaches a verdict
- **THEN** applicable implementation, verification, security, and review evidence files identify commands, results, revision/scope, and known limitations

### Requirement: Project state is updated only after accepted work
`docs/PROJECT_STATE.md` SHALL summarize current milestone, accepted capabilities, active tasks, blockers, decisions, debt, next task, demo readiness, and risks, and MUST NOT claim unreviewed implementation as complete.

#### Scenario: Task receives PASS
- **WHEN** a remediation task is independently accepted
- **THEN** Beads, OpenSpec task status, and project state are updated coherently

### Requirement: Agent instructions are DX-OS-owned and encoding-clean
Root and project agent instructions SHALL describe DX-OS, defer feature contracts to OpenSpec, point task execution to Beads, preserve user authority, and contain valid UTF-8 text.

#### Scenario: Fresh agent enters the new repository
- **WHEN** it reads root guidance and current project state
- **THEN** it can identify the product, authority order, current change, next ready issue, validation commands, and prohibited actions without Elsa-specific instructions

### Requirement: Business work remains gated by bootstrap READY
No business feature spec or implementation SHALL begin until the follow-up bootstrap audit records `READY` and the Beads re-audit task is closed with evidence.

#### Scenario: Product feature is proposed before READY
- **WHEN** bootstrap has any required gate not passing
- **THEN** the feature remains deferred and remediation continues

### Requirement: OSS governance is durable project truth
Agents MUST preserve the public-source ownership, OSS license, source reproducibility, dependency transparency, attribution, service disclosure, AI provider independence, truthful claims, competition evidence, and release blockers defined by the constitution and bootstrap change.

#### Scenario: An implementation weakens OSS obligations
- **WHEN** an agent proposes a private-source dependency, concealed reuse, vendor-locked domain contract, incomplete disclosure, or unsupported "100% open source" claim
- **THEN** review returns `FIX_REQUIRED`
- **AND** no Beads closure, OpenSpec completion, release, or READY transition is permitted
