# Leave Planner agent guide

This file applies to the solution root and all projects below it. Resolve paths from this directory. Keep code identifiers, public API text, and user-facing documentation in English.

## Start here

1. Read relevant code and [README.md](README.md) before changing behavior. Check `git status` so existing user changes are preserved.
2. Follow the latest user request. If it conflicts with an established product rule or public contract, explain the impact. Do only the requested work; a documentation task does not authorize application changes.
3. Read a focused guide when its keywords match the task. Follow its links to relevant source and tests.

| Keywords / work area | Read |
| --- | --- |
| Architecture, dependency, refactor, new class, strategy, DI | [Architecture and ownership](docs/agent-guides/architecture.md) |
| Calendar, holiday, budget, ranking, explanation, product behavior | [Planning rules](docs/agent-guides/planning-rules.md) |
| Nager, HTTP, JSON, country, subdivision, validation, status code, API | [Provider and API contract](docs/agent-guides/provider-and-api.md) |
| Test, edge case, verification, completion, demo | [Verification](docs/agent-guides/verification.md) and [edge-case matrix](tests/EDGE_CASES.md) |

## Project at a glance

`LeavePlanner.sln` contains `LeavePlanner.Api`, `LeavePlanner.Application`, `LeavePlanner.Domain`, `LeavePlanner.Infrastructure`, and matching projects under `tests/`. All target .NET 9. The API is a controller-based ASP.NET Core application with OpenAPI. Its endpoint is `POST /api/leave-plans/recommendation`; it supports `longestBreak` (default), `minimumLeave`, and `bestEfficiency`. See [README.md](README.md) for the runnable contract and [LeavePlanner.http](LeavePlanner.http) for requests.

The product recommends **one continuous break** within the user's date range and leave budget, with exact leave dates and a reason. It is planning advice, not company leave approval. Preserve the separation between pure planning rules, application orchestration, provider I/O, and HTTP contracts. Keep the response deterministic for the same input and holiday data.

## Work on this repository

- Make the smallest change that fully meets the request. Preserve the existing four-layer solution and public contract unless the request calls for a change. Avoid speculative abstractions, extra projects, a database, frontend, authentication, or unrelated product features.
- For behavior changes, identify the affected rule and boundary, add or update meaningful network-independent tests, implement, and update README or `.http` if public behavior changes. Keep controllers thin and errors safe for clients.
- For provider changes, verify the current official Nager contract and representative response before changing parsing or assumptions. Never manufacture missing holiday data or return a partial recommendation after a failed year.
- Before delivery, inspect the diff for responsibility, dependency direction, names, duplicated rules, and contract consistency. Run checks appropriate to the change; document-only edits need content and link checks, not a build.
- Report changed files, behavior, checks actually run, and any unverified risk. Do not claim tests or live data were verified without evidence. Do not commit, publish, or deploy unless asked.

Run implementation checks from the solution root:

```powershell
dotnet restore .\LeavePlanner.sln
dotnet build .\LeavePlanner.sln --no-restore
dotnet test .\LeavePlanner.sln --no-build
```

An optional live smoke request uses `LeavePlanner.http` after `dotnet run --project .\LeavePlanner.Api\LeavePlanner.csproj --launch-profile http`. It is separate from the automated suite.
