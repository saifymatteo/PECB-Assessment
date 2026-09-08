# Support Desk

A small internal tool for a support team to track customer issues from the moment they are
reported until they are closed — built for the PECB Full-Stack Developer (.NET & Angular)
technical assignment.

- **Backend**: ASP.NET Core (`.NET 10`) Web API, EF Core 10 + PostgreSQL (Npgsql), xUnit
- **Frontend**: Angular 20 (standalone components, signals, RxJS), Angular Material, Jasmine/Karma

---

## Running it locally

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) (for the PostgreSQL database)
- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js 20+ and npm

### 1. Database

```bash
docker compose up -d
```

Starts PostgreSQL 17 on `localhost:5432` (user/password/db: `supportdesk` / `supportdesk` / `supportdesk`).

### 2. Backend API

```bash
cd backend/src/SupportDesk.Api
dotnet run
```

On startup (development) the API applies EF Core migrations and seeds demo data
(5 agents, 20 tickets across all statuses/priorities, several of them overdue) — no extra setup.
It listens on **http://localhost:5000**; an OpenAPI document is served at `/openapi/v1.json`.

Run the backend tests:

```bash
cd backend
dotnet test
```

### 3. Frontend

```bash
cd frontend
npm install
npm start
```

Opens on **http://localhost:4200**; a dev proxy (`proxy.conf.json`) forwards `/api/*`
to the backend, so no CORS configuration is needed.

Run the frontend tests:

```bash
cd frontend
npx ng test --watch=false --browsers=ChromeHeadless
```

(Requires Chrome; Jasmine/Karma is configured out of the box.)

---

## Where the business rules live — and why

All seven business rules are enforced **on the backend**, in the **domain entities**
(`backend/src/SupportDesk.Api/Domain`), not in controllers or the database:

| Rule | Where |
|------|-------|
| 1. Due date derived from priority; recalculated from the *original creation date* on priority change | `Ticket.Create` / `Ticket.UpdateDetails` |
| 2. Status transition graph (incl. reopening, no skipping, Closed is terminal) | `Ticket.TransitionTo` + `Ticket.AllowedTransitions` |
| 3. In Progress requires an assigned **active** agent (also when reopening) | `Ticket.TransitionTo` → `RequireActiveAgentForInProgress` |
| 4. Inactive agents cannot be assigned | `Ticket.AssignAgent` |
| 5. Closed ticket is read-only (edits, status, comments — and deletion, see assumptions) | `Ticket.*` guards throwing `TicketClosedException` |
| 6. Resolved/closed timestamps set by the system | `Ticket.TransitionTo` |
| 7. Overdue = due date passed ∧ status ∉ {Resolved, Closed} | `Ticket.IsOverdue` (+ SQL-level filter in the list query) |

**Why:** the rules are invariants of the ticket, not of an HTTP request. Putting them on the
entity means *every* entry point (API, seeder, future importers) is safe by construction,
the rule set is readable in one file, and the rule tests run as pure unit tests without a
database (`tests/SupportDesk.Api.Tests`). Application services (`Features/`) only
orchestrate load → entity method → save; controllers contain no rule logic. See
`docs/adr/0002-rules-on-domain-entities.md` for the trade-offs.

### Why status changes have a dedicated endpoint

`POST /api/tickets/{id}/status` is the only way to change a status; the generic update
endpoint refuses it. A status change is a *workflow event*: it has preconditions (transition
graph, active-agent rule) and side effects (system-set timestamps), so it gets its own
endpoint, its own DTO (a bare `status` field — clients cannot smuggle in `resolvedAt` or
`dueDate`), and one obvious place for validation. Details in `docs/adr/0003-dedicated-status-endpoint.md`.

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

---

## Assumptions

Where the assignment text left room, these calls were made deliberately:

1. **Deleting a Closed ticket is rejected** (409 `TICKET_CLOSED`). "Read-only" is taken
   literally: a closed ticket is immutable, deletion included.
2. **Unassigning/reassigning is allowed in any non-Closed status.** Rule 3 only gates
   *entering* In Progress; an In-Progress ticket whose agent is removed stays In Progress.
3. **Priority can be edited while Resolved**, and the due date is then recalculated from
   the original creation date. This is harmless because Resolved/Closed tickets are never
   overdue (rule 7).
4. **Reopening clears `ResolvedAt`** so the timestamp always reflects the latest resolution;
   it is set again when the ticket is resolved again.
5. **No authentication/authorization** — not part of the assignment; noted as future work.
6. Agents have list/search/get/create/activate-deactivate (no edit/delete UI) — the minimum
   needed to demonstrate rule 4 end-to-end.
7. `DateTime.UtcNow` everywhere; PostgreSQL `timestamptz` columns.
8. Seed dates are relative to seeding time so some tickets are always overdue on a fresh run.

## What I would improve with more time

- **Integration tests** with a real database (e.g. Testcontainers) covering the API layer end-to-end, on top of the current pure domain unit tests and persistence tests.
- **Audit trail**: record who changed a status/assignment and when (event log table).
- **Concurrency**: optimistic concurrency tokens (row versioning) for simultaneous edits.
- **SignalR** (or polling) so the list updates when other agents work tickets.
- **Sorting** options on the ticket list and saved filter views.
- **Agent management UI** (create/deactivate from the frontend) — the API supports it already.
- **CI pipeline** running both test suites; containerized frontend build.

## Time spent

The working session started **around 12:30 PM MYT on 8 September 2026** and this state of the
solution was reached a bit over **one hour** later, including tests, seed data and this README.
(_Update this line when you submit if more polish time was spent._)

---

## Project structure

```
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
compose.yml             PostgreSQL for local development
docs/adr/               architecture decision records
```
