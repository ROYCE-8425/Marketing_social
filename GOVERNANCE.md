# Project Governance

DX-OS is an independent open-source project dedicated to transparent, community-driven development with clear ownership, architectural discipline, and open governance.

---

## 1. Governance Model

DX-OS operates under a **Benevolent Maintainer / Consensus-Driven** governance model:
- **Project Owner (PO)**: Holds ultimate responsibility for project vision, trademark stewardship, and release authority.
- **Core Maintainers**: Experienced contributors with commit access, responsible for reviewing PRs, managing issues, and maintaining code quality.
- **Contributors**: Community members who submit issues, pull requests, documentation, and participate in discussions.

---

## 2. Decision Making Process

1. **Everyday Changes**: Bug fixes, minor improvements, and documentation updates are reviewed by any maintainer and merged upon passing all automated quality gates.
2. **Major Architectural Changes**: New module additions, dependency alterations, or security boundary shifts require an **Architectural Decision Record (ADR)** under `docs/adr/`. Decisions are discussed in GitHub Discussions/Issues and require consensus among core maintainers.
3. **OpenSpec Governance**: Structured change management follows OpenSpec specifications (`openspec/`) to track requirements, design, and verification tasks before implementation.

---

## 3. Release Authority

- Official releases and GitHub tags are issued exclusively by the Project Owner and Core Maintainers following the strict [Release Criteria](docs/RELEASE_CRITERIA.md).
- No release may be cut without passing the complete quality gate suite (`scripts/check.ps1 -Profile ReadyAudit`) and verifying zero high/critical security findings.

---

## 4. Becoming a Maintainer

Active, constructive contributors who consistently submit high-quality PRs, conduct thorough reviews, and assist community members may be invited to join as Core Maintainers upon unanimous agreement of current maintainers.

---

## 5. Amendments

Amendments to this governance document must be submitted as a pull request and approved by the Project Owner.
