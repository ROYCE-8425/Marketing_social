# Remaining Operational Gaps & Post-Launch Roadmap

**Task ID**: `FOSS-CONVERSION-001`  
**Execution Date**: 2026-08-19  
**Current State**: Local Codebase 100% Prepared and Verified

---

## 1. Remote GitHub Configuration Tasks (Pending 2FA Session)

The following operational tasks require web browser interaction with GitHub 2FA authentication or GitHub PAT:

1. **Repository Visibility**:
   - Change repository visibility from `Private` to `Public` in GitHub Settings -> Danger Zone -> Change repository visibility.
2. **Metadata & About Section**:
   - Set Description: `Open-source AI-native operating system for SME marketing workflows, automation, analytics and governed AI agents.`
   - Set Topics: `dotnet, aspnet-core, csharp, postgresql, workflow-automation, elsa-workflows, open-source, marketing-automation, ai-agents, sme`
3. **Feature Toggles**:
   - Confirm `Issues` and `Discussions` are checked in General settings.
4. **Branch Protection Ruleset**:
   - Apply ruleset to `main` requiring PR review, passing `validate` CI status check, and blocking force pushes.
5. **Initial Release Tag**:
   - Cut and publish Git tag `v0.1.0-alpha` with release notes from `CHANGELOG.md`.

---

## 2. Technical Roadmap Gaps (Tracked in `ROADMAP.md`)

1. **Embedded Elsa Studio Dashboard**:
   - Visual workflow designer UI package integration planned for v0.2.0.
2. **Marketing Connectors**:
   - Outbound Email, Webhook handlers, and Social publishing activities planned for v0.2.0.
3. **Governed AI Gateway**:
   - Multi-provider LLM gateway with prompt logging and token limits planned for v0.2.0.
