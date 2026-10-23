# Bookable — resource booking

A web app where employees book meeting rooms, pool cars and catering, and admins manage
users, resources, booking constraints and utilisation. Built for the Aries engineering
case study.

> **Status: skeleton.** Full README (run instructions, architecture decisions, AI
> workflow, what's next) lands with the final submission.

## Run locally

```bash
docker compose up -d        # Postgres on localhost:5455
cd Api && dotnet run        # API on http://localhost:5214 — migrates + seeds demo data
cd client && npx ng serve   # SPA on http://localhost:4200
```

Stack: .NET 10 · EF Core · PostgreSQL 17 · SignalR · Angular 22 (signals, standalone) ·
Tailwind v4 · xUnit + Testcontainers.
