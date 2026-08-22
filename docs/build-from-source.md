# Building DX-OS from Source

This document provides complete, reproducible instructions for building DX-OS from source across Windows, Linux, and macOS.

---

## Prerequisites

1. **.NET 10.0 SDK**:
   - Install the official [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) matching `global.json` (`10.0.100` or higher preview).
   - Verify installation:
     ```bash
     dotnet --version
     ```
2. **Git**:
   - Required to clone and track the source repository.
3. **PowerShell** (optional for scripts):
   - Windows PowerShell 5.1+ or cross-platform PowerShell Core (`pwsh`).
4. **Docker** (optional for integration testing and container builds):
   - Docker Desktop or Docker Engine with Docker Compose.

---

## Step-by-Step Build Instructions

### 1. Clone the Official Repository

This GitHub repository name is `open_source`; the project is DX-OS:

```bash
git clone https://github.com/ROYCE-8425/Marketing_social.git dx-os
cd dx-os
```

### 2. Restore Dependencies in Locked Mode

DX-OS enforces Central Package Management (CPM) with deterministic `packages.lock.json` files for all 9 projects:

```bash
dotnet restore --locked-mode
```

*Note: If package references are modified in `Directory.Packages.props`, lockfiles must be updated deliberately via `dotnet restore --force-evaluate`.*

### 3. Compile the Solution

Compile in `Release` configuration with compiler warning enforcement (`TreatWarningsAsErrors`):

```bash
dotnet build -c Release --no-restore
```

Expected output:
```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Running Automated Tests

### Unit Tests
Execute the xUnit.net v3 test runner:

```bash
dotnet test tests/DXOS.Unit.Tests -c Release --no-build
```

### Architecture Rule Tests
Validate architectural layer boundaries and dependencies with ArchUnitNET:

```bash
dotnet test tests/DXOS.Architecture.Tests -c Release --no-build
```

### Database Integration Tests
Execute integration tests using Testcontainers PostgreSQL:

```bash
# Ensure Docker is running
dotnet test tests/DXOS.Integration.Tests -c Release --no-build
```

---

## Building Container Images Locally

You can build the production container image using Docker:

```bash
docker build -t dxos-api:latest -f Dockerfile .
```

To run the containerized API:

```bash
docker run --rm -p 5000:8080 -e ConnectionStrings__PostgreSql="Host=host.docker.internal;Database=dxos;Username=postgres;Password=your_password" dxos-api:latest
```

---

## Troubleshooting

- **SDK Version Mismatch**: Ensure your installed .NET SDK matches or is compatible with `global.json`.
- **Lockfile Out of Sync**: If you receive a lockfile mismatch error during restore, run `dotnet restore --force-evaluate` only after reviewing dependency version changes.
- **Docker Daemon Unavailable**: Ensure Docker Desktop is running before launching integration tests or `docker compose`.
