# School Admission System

A first-come, first-served school admission application built with .NET 8, PostgreSQL, React 18 + Vite, and Docker Compose.

## Run with Docker (recommended)

Requirements: Docker Engine and the Docker Compose plugin. No local .NET, Node.js, or PostgreSQL installation is needed.

```bash
cp .env.example .env
docker compose up --build
```

- Frontend: http://localhost:3000
- Backend API: http://localhost:8080
- Health check: http://localhost:8080/health

PostgreSQL data is persisted in the `postgres_data` volume. To stop containers without deleting data, run `docker compose down`. To delete the database too, run `docker compose down -v`.

## Configuration

| Variable | Default | Description |
|---|---|---|
| `DATABASE_URL` | Compose-provided PostgreSQL URL | Backend PostgreSQL connection string |
| `HOLD_DURATION_SECONDS` | `600` | Seat reservation duration |
| `POSTGRES_PORT` | `5432` | Host port for PostgreSQL (useful for running integration tests) |
| `VITE_API_BASE_URL` | `http://localhost:8080` | API origin used by the browser (frontend build argument) |

If `VITE_API_BASE_URL` changes, rebuild the frontend image: `docker compose build frontend`.

## Main API

| Method | Path | Description |
|---|---|---|
| `POST` | `/classes` | Create a class/shift `{ "name": "Class 6 - Morning Shift", "totalSeats": 120 }` |
| `GET` | `/classes` | List classes and seat counts |
| `GET` | `/classes/{classId}` | Get one class and seat counts |
| `POST` | `/classes/{classId}/holds` | Reserve a seat; requires `X-Student-Id` |
| `GET` | `/holds/{holdId}` | Get reservation status |
| `POST` | `/holds/{holdId}/confirm` | Simulate successful payment; requires `X-Student-Id` |
| `DELETE` | `/holds/{holdId}` | Cancel a reservation; requires `X-Student-Id` |
| `GET` | `/me/holds` | List a student's applications; requires `X-Student-Id` |

The student ID must be exactly 17 digits. Hold statuses are `HELD`, `CONFIRMED`, `EXPIRED`, and `CANCELLED`.

## Automated backend tests

Tests are PostgreSQL integration tests. Start the database from Compose, create a separate disposable test database, then set `TEST_DATABASE_URL` and run:

```bash
docker compose up -d db
docker compose exec db createdb -U postgres school_admission_test
cd backend
TEST_DATABASE_URL="Host=localhost;Port=5432;Database=school_admission_test;Username=postgres;Password=postgres" dotnet test SchoolAdmission.Tests/SchoolAdmission.Tests.csproj
```

In PowerShell, set the variable first with `$env:TEST_DATABASE_URL="Host=localhost;Port=5432;Database=school_admission_test;Username=postgres;Password=postgres"`, then run `dotnet test SchoolAdmission.Tests/SchoolAdmission.Tests.csproj`.

The suite checks class creation, idempotent confirmation, and concurrent reservation requests (40 requests against a five-seat class).

## Project structure

- `backend/SchoolAdmission.Api` — .NET 8 API and EF Core PostgreSQL persistence
- `backend/SchoolAdmission.Tests` — automated API integration tests
- `frontend` — React parent/admin UI
- `docker-compose.yml` — PostgreSQL, API, and frontend orchestration

See [ASSUMPTIONS.md](ASSUMPTIONS.md) for decisions where the assignment leaves behavior open, and [AI_USAGE.md](AI_USAGE.md) for AI assistance disclosure.
