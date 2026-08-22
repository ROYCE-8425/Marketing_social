# DX-OS Project Roadmap

This document outlines the strategic milestones and technical roadmap for the DX-OS open-source project.

---

## Current Status: Bootstrap Remediation (Milestone BR001)

- [x] **BR001-R1: Provenance & Licensing**: Independent DX-OS repository, Apache-2.0 canonical license, REUSE compliance.
- [x] **BR001-R2: Deterministic Builds**: .NET 10 compilation with CPM package locking and zero compiler warnings.
- [x] **BR001-R3: Quality Gate Automation**: 26 fail-fast local and CI quality gates (`scripts/check.ps1`).
- [x] **BR001-R4: Runtime Orchestration**: Docker Compose and .NET Aspire 13.4 orchestration with PostgreSQL 18.4.
- [x] **BR001-R5: Comprehensive Testing**: xUnit.net v3 unit tests, ArchUnitNET architecture rules, Testcontainers integration tests.
- [x] **BR001-R6: Governance & OpenSpec**: 7 Architectural Decision Records, OpenSpec change management.
- [x] **BR001-R7: Supply Chain & Security**: Gitleaks secret scans, Trivy container analysis, Syft CycloneDX SBOM, Grype vulnerability scans.
- [ ] **BR001-R8: Public Audit & Verified Release**: Clean clone audit, OpenSSF Scorecard badges, and initial release tag.

---

## Near-Term Roadmap (v0.2.0 - Core Usability)

### 1. Embedded Visual Workflow Designer
- Embed and configure Elsa Studio web interface for visual workflow creation and real-time execution monitoring.
- Provide pre-packaged marketing workflow templates (Lead Nurturing, Drip Campaign, Churn Alert).

### 2. Marketing Channel Connectors
- **Email Gateway**: SMTP, SendGrid, and Mailgun outbound activities with open/click tracking.
- **Webhook Subscriptions**: Incoming webhook triggers for external CRM events (HubSpot, Salesforce, Shopify).
- **Social Media Publishing**: Connector abstractions for scheduled automated posts.

### 3. Governed AI Execution Gateway
- Policy-bounded LLM activities supporting local models (Ollama, vLLM) and optional hosted providers (Gemini, OpenAI).
- Prompt template versioning, token budget caps, and full execution audit trails in PostgreSQL.

---

## Medium-Term Roadmap (v0.3.0+ - Scale & Intelligence)

### 1. Multi-Tenant SME Isolation
- Workspace-based tenant partitioning for agencies and multi-brand businesses.
- Role-based access control (RBAC) for marketing teams and external clients.

### 2. Marketing Analytics & ROI Tracking
- Integrated campaign performance dashboard (conversion rates, CAC, attribution models).
- Event streaming integration with PostgreSQL analytical queries.

### 3. Autonomous AI Agent Teams
- Multi-agent collaborative workflows: Copywriter, Visual Designer, Fact Checker, Compliance Auditor.
- Human-in-the-loop approval checkpoints before external publication.
