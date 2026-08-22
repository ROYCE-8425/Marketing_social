# Authority Hierarchy

1. Current User Instruction
2. DX-OS Constitution (.specify/memory/constitution.md)
3. Accepted OpenSpec Contracts (openspec/)
4. Architecture Decision Records (docs/adr/)
5. Business Rules
6. Beads Task Acceptance Criteria (bd)
7. Workspace Rules & Repository Instructions (.agents/rules/, AGENTS.md)
8. Implementation Artifacts & Reports (artifacts/task-runs/)
9. Model Assumptions / Chat Transcripts

If two sources at the same or higher level conflict:
- Stop immediately.
- Report the conflict explicitly.
- Do not silently choose an outcome.

## Role Boundaries

**Codex (Reviewer):**
- Inspects OpenSpec contracts, architecture, ADRs, Beads decomposition, and acceptance criteria.
- Conducts independent verification of git diffs, build outputs, test results, security reports, and runtime traces.
- Issues final PASS or FIX_REQUIRED verdict.

**Gemini (Implementer):**
- Claims assigned Beads tasks and implements scoped code/test changes.
- Executes local verification gates and records structured evidence.
- Must NOT approve own implementation, mark OpenSpec checkboxes, close Beads issues, or commit/merge without Codex PASS.