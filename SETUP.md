# Setup — DX-OS Marketing

Two stacks live in this repository. They use **different ports** so they can run on one machine.

## 1. DX-OS product OS (default)

Requires Docker. Optional: .NET 10 SDK for building from source.

```bash
cp .env.example .env
# set POSTGRES_PASSWORD

docker compose -f compose.yaml up -d --build
curl -f http://localhost:8080/health
```

Operator board: http://localhost:8080/

Full instructions: [docs/getting-started.md](docs/getting-started.md), [docs/build-from-source.md](docs/build-from-source.md).

## 2. AIECOS Social CRM foundation (optional)

Pancake DOM ingest + static admin UI. MIT-licensed. No Facebook/TikTok/Zalo app credentials required.

```bash
docker compose -f docker-compose.yml up -d
# Admin UI      http://localhost:8081
# PostgREST     http://localhost:3000
# Sync receiver http://localhost:3500/api/status
# Postgres      localhost:5433  (user/pass postgres/postgres)
```

Then Settings in the UI: Supabase URL `http://localhost:3000`, schema `aiecos_social`.

Original AIECOS guide: [docs/aiecos/SETUP.md](docs/aiecos/SETUP.md).

## License

- DX-OS (`src/`, `tests/`, `compose.yaml`, most docs): Apache-2.0
- AIECOS folders: MIT — [LICENSES/MIT-AIECOS.txt](LICENSES/MIT-AIECOS.txt)
