# Getting Started with DX-OS

This guide helps you run DX-OS quickly on your local machine using Docker Compose or .NET Aspire.

---

## Prerequisites

Ensure you have installed:
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) or Docker Engine (v24.0+)
- [Git](https://git-scm.com/)
- Optional: [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) if running outside Docker

---

## Option 1: Quick Launch with Docker Compose (Recommended)

Docker Compose provides the fastest, all-in-one setup including the DX-OS API and a configured PostgreSQL 18.4 database.

### 1. Clone the Official Repository

This GitHub repository name is `open_source`; the project is DX-OS:

```bash
git clone https://github.com/ROYCE-8425/open_source.git dx-os
cd dx-os
```

### 2. Configure Environment (Optional)

You can customize runtime settings in `.env` (copy from `.env.example` if desired):

```bash
cp .env.example .env
```

### 3. Start the Services

```bash
docker compose up -d --build
```

### 4. Verify the Services

Check that both the PostgreSQL container and DX-OS API container are healthy:

```bash
# Check container status
docker compose ps

# Check API health endpoint
curl -f http://localhost:5000/health
```

Expected response: `HTTP 200 OK` (Healthy).

### 5. Stopping the Environment

```bash
docker compose down
```

---

## Option 2: Running with .NET Aspire

For inner-loop development with distributed logging and dashboard metrics:

```bash
# 1. Navigate to the AppHost directory
cd src/DXOS.AppHost

# 2. Run Aspire orchestrator
dotnet run
```

Aspire will launch the local PostgreSQL container resource, start `DXOS.Api`, and open the Aspire Dashboard URL displayed in your terminal console.

---

## Next Steps

- Learn how to compile the source code: [Building from Source](build-from-source.md)
- Explore the developer workflow and testing: [Development Guide](development.md)
- Learn about DX-OS architectural decisions: [Architecture Decisions](adr/README.md)
