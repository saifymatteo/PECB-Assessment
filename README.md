# Support Desk

A small internal tool for a support team to track customer issues from the moment they are
reported until they are closed. Built for the PECB Full-Stack Developer (.NET & Angular)
technical assignment.

- **Backend**: ASP.NET Core (`.NET 10`) Web API, EF Core 10 + PostgreSQL (Npgsql), xUnit
- **Frontend**: Angular 20 (standalone components, signals, RxJS), Angular Material, Jasmine/Karma

## Getting Started

Two ways to run the app, one Compose file each. Both serve the UI on **<http://localhost:4200>**.

| | Compose file | Starts | Needs .NET / Node |
| --- | --- | --- | --- |
| **Docker** | `compose.yml` | PostgreSQL + API + UI | no |
| **Local** | `compose.db.yml` | PostgreSQL only | yes |

`compose.yml` `include`s `compose.db.yml` instead of repeating the database, so both modes share
the same project name (`supportdesk`), the same `supportdesk-db` container and the same data
volume: switching mode keeps the data.

> Note: only run compose at a time, do not run both `compose.yml` and `compose.db.yml` at the same time.

### Docker

```bash
docker compose up --build -d
```

One command brings up PostgreSQL, the API (which applies migrations and seeds
demo data on startup) and an nginx container that serves the built Angular app and proxies
`/api/*` to the API container. Browse to **<http://localhost:4200>**.

Tear down with `docker compose down` (add `-v` to also reset the database volume).

### Local development (hot reload)

Prerequisites:

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js 20+ and npm
- PostgreSQL server - through [Docker](https://docs.docker.com/get-docker/).

#### **1. Database**

```bash
docker compose -f compose.db.yml up -d
```

Starts PostgreSQL 17 on `localhost:5432` (user/password/db: `supportdesk` / `supportdesk` / `supportdesk`)
and nothing else. Skip this step if you already have a PostgreSQL reachable with the credentials in
`backend/src/SupportDesk.Api/appsettings.json`.

To stop, run `docker compose -f compose.db.yml down` (add `-v` to also reset the data).

#### **2. Backend API**

```bash
cd backend/src/SupportDesk.Api
dotnet run
```

On startup the API applies EF Core migrations and seeds demo data (5 agents, 20 tickets across all statuses/priorities, several of them overdue).
It listens on **<http://localhost:5000>**; an OpenAPI document is served at `/openapi/v1.json` in Development.

Run the backend tests:

```bash
cd backend
dotnet test
```

#### **3. Frontend**

```bash
cd frontend
npm install
npm start
```

Opens on **<http://localhost:4200>**. The dev proxy is wired into `angular.json`
(`proxy.conf.json` forwards `/api/*` to the backend), so it works with plain `ng serve`
as well.

Run the frontend tests:

```bash
cd frontend
npx ng test --watch=false --browsers=ChromeHeadless
```

## Business Rule

All seven business rules are enforced **on the backend**, in the **domain entities**
(`backend/src/SupportDesk.Api/Domain`):

| Rule | Where |
| ------ | ------- |
| 1. Due date derived from priority; recalculated from the *original creation date* on priority change | `Ticket.Create` / `Ticket.UpdateDetails` |
| 2. Status transition graph (incl. reopening, no skipping, Closed is terminal) | `Ticket.TransitionTo` + `Ticket.AllowedTransitions` |
| 3. In Progress requires an assigned **active** agent (also when reopening) | `Ticket.TransitionTo` → `RequireActiveAgentForInProgress` |
| 4. Inactive agents cannot be assigned | `Ticket.AssignAgent` |
| 5. Closed ticket is read-only (edits, status, comments and deletion, see assumptions) | `Ticket.*` guards throwing `TicketClosedException` |
| 6. Resolved/closed timestamps set by the system | `Ticket.TransitionTo` |
| 7. Overdue = due date passed status {Resolved, Closed} | `Ticket.IsOverdue` (+ SQL-level filter in the list query) |

### Dedicated endpoint for status changes

`POST /api/tickets/{id}/status` is the only way to change a status; the generic update
endpoint refuses it. A status change is a *workflow event*: it has preconditions (transition
graph, active-agent rule) and side effects (system-set timestamps), so it gets its own
endpoint, its own DTO (a bare `status` field - clients cannot smuggle in `resolvedAt` or
`dueDate`), and one obvious place for validation.

### Error contract

All errors are RFC 7807 `application/problem+json` responses with a stable machine-readable
`errorCode`, e.g.:

```json
{
  "title": "Invalid status transition",
  "status": 409,
  "detail": "Cannot change ticket status from 'New' to 'Resolved'.",
  "errorCode": "INVALID_TRANSITION",
  "allowedTransitions": ["InProgress"],
  "traceId": "..."
}
```

Codes: `INVALID_TRANSITION` (409), `TICKET_CLOSED` (409), `AGENT_ASSIGNMENT_REQUIRED` (409),
`AGENT_INACTIVE` (422), `EMAIL_ALREADY_IN_USE` (409), `NOT_FOUND` (404), field-level
`errors` on validation failures (400). Stack traces never reach the client.

## Assumptions

Unclear spec from the assessment, these were made deliberately:

1. **Deleting a Closed ticket is rejected** (409 `TICKET_CLOSED`). "Read-only" is done
   literally: a closed ticket is immutable, deletion included.
2. **Unassigning/reassigning is allowed in any non-Closed status.** Rule 3 only gates
   *entering* In Progress; an In-Progress ticket whose agent is removed stays In Progress.
3. **Priority can be edited while Resolved**, and the due date is then recalculated from
   the original creation date. This is harmless because Resolved/Closed tickets are never
   overdue (rule 7).
4. **Reopening clears `ResolvedAt`** so the timestamp always reflects the latest resolution;
   it is set again when the ticket is resolved again.
5. Agents have list/search/get/create/activate-deactivate (no edit/delete UI) - the minimum
   needed to demonstrate rule 4 end-to-end.
6. `DateTime.UtcNow` everywhere; PostgreSQL `timestamptz` columns.
7. Seed dates are relative to seeding time so some tickets are always overdue on a fresh run.

## What can be improve

- **Integration tests** with a real database (e.g. Testcontainers) covering the API layer end-to-end, on top of the current pure domain unit tests and persistence tests.
- **Audit trail**: record who changed a status/assignment and when (event log table).
- **Concurrency**: optimistic concurrency tokens (row versioning) for simultaneous edits.
- **SignalR** (or polling) so the list updates when other agents work tickets.
- **Sorting** options on the ticket list and saved filter views.
- **Agent management UI** (create/deactivate from the frontend) - the API supports it already.
- **CI pipeline** running both test suites; containerized frontend build.

## Time spent

The working session started **around 12:30 PM MYT on 8 September 2026** with on and off work. Finished at **10:00 PM MYT on 9 September 2026**.

## Project structure

```directory
backend/
  src/SupportDesk.Api/
    Domain/        entities + enums + business rules + domain exceptions
    Features/      application services (orchestration) + DTO mapping
    Contracts/     request/response DTOs with DataAnnotations validation
    Infrastructure/  EF Core (AppDbContext, configurations, migration, seeder)
    Controllers/   thin HTTP endpoints
    Domain/…/BusinessRuleException.cs → ProblemDetails via middleware
  tests/SupportDesk.Api.Tests/   33 unit tests (pure domain + SQLite persistence)
frontend/                         Angular 20 standalone app
  src/app/core/         models, HTTP services, API-error mapping, labels
  src/app/features/     ticket-list, ticket-detail, ticket-dialog
compose.yml             Full-stack: PostgreSQL + API + nginx-hosted UI, for one-command Docker deployment
compose.db.yml          PostgreSQL only - what local development runs; included by compose.yml
```
