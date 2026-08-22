# DX-OS Release and Competition Criteria

Status: NOT_READY

DX-OS must not be marked READY or competition-release-ready until an independent CLEAN CLONE / READY audit proves every required gate for the exact revision.

## Mandatory release evidence

- DX-OS-owned public repository, independent history, README, license, release metadata, and CI.
- Canonical Apache-2.0 license or a verified superseding OSI-compatible license ADR.
- Clean clone -> locked restore -> source build -> infrastructure -> DX-OS start -> documented demo evidence without private source.
- `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md`, reused-source provenance, and `artifacts/sbom.cdx.json` reconciled to the deliverable.
- Separate third-party-service disclosure including development tools and all demo/runtime services.
- Installation, Docker/Compose deployment, source documentation, security results, and exact CI artifacts.
- Truthful language distinguishing project source, core runtime, OSS dependencies, services, and development tooling.

## Unconditional blockers

Release fails when the DX-OS OSS license is missing, attribution is incomplete, the SBOM is missing or stale, a required service is undisclosed, the clean-clone path fails, public-source ownership is absent, or the application depends on undisclosed private source.

No exception, warning-only result, `continue-on-error`, or manual narrative may convert one of these blockers into PASS.
