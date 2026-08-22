# Verification Evidence (BR001-R1 Fix)

## Path Safety
- Source: `C:\Users\199X\OneDrive\Máy tính\olympic\open_source`
- Target: `C:\Users\199X\OneDrive\Máy tính\olympic\dx-os`
- Evaluated completely separate directories. 

## Manifest Validation
- Full manifest SHA-256 round-trip confirmed. Total Tracked Files: 95. Every tracked file has exactly one manifest action (either `copy` or `recreate`).
- External-Final-Hash Policy applied to self-referential evidence files.

## Git Identity
- Commits: 1
- History: The commit history is maintained at exactly 1 initial extraction commit. The final commit ID is recorded in the old-checkout verification transcript to avoid self-referential hashing loops.

## Solution and Project Status
- Projects in `DXOS.slnx`: verified to only contain the 10 DX-OS projects.
- Reference checks: No Elsa references or `bin/obj` directories were found in any `.csproj` file in the new repository.

## Final Non-Mutating Verification Matrix
All compound validation checks (path reconciliation, size/hash checks, paired evidence equality, UTF-8 validation, solution integrity, and Beads linking/cycles) are performed deterministically by the old-checkout-only verifier script `artifacts/task-runs/open_source-cab.1/verify-r1.ps1` (SHA-256: `8B3F840747C6E1EEAA322C56E3ACE3E11B2976970A452EC05C09EF92B6059C8D`).

Exact command strings, actual exit codes, measured durations, and outputs are recorded in the old-only `artifacts/task-runs/open_source-cab.1/post-commit-transcript.md`.

| Verification Step | Exact Command |
|-------------------|---------------|
| **Git identity/count/status/remotes** | `git rev-parse HEAD`, `git rev-list --count HEAD`, `git status --short`, `git remote -v` |
| **Beads epic link** | `bd.cmd show open_source-cab --json` |
| **Beads children** | `bd.cmd list --parent open_source-cab --json` |
| **Beads cycles** | `bd.cmd dep cycles` |
| **Solution checks** | `dotnet sln DXOS.slnx list` |
| **Reference/forbidden-path checks** | `Get-ChildItem -Recurse -Filter *.csproj \| Select-String -Pattern "Elsa"` |
| **Bundle verification** | `git bundle verify artifacts/task-runs/open_source-cab.1/dx-os-backup.bundle` |
| **Old-checkout identity** | `git rev-parse HEAD`, `git status --short` in `open_source` |
