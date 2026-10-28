# Architecture and ownership

Read this guide for architecture, dependency, refactor, new class, strategy, or dependency injection work. The current solution uses four small projects; preserve their responsibilities and dependency direction.

| Project | Owns | Depends on |
| --- | --- | --- |
| `LeavePlanner.Domain` | `LeaveCalendar`, candidate and selection models, `PlanningPriority`, pure ranking strategies | No application project |
| `LeavePlanner.Application` | `LeavePlanningService`, holiday normalization, strategy registry, explanations, planning errors and I/O ports (`IHolidaySource`, `ISubdivisionCatalog`) | Domain |
| `LeavePlanner.Infrastructure` | Nager HTTP transport and v4 parsing, ISO subdivision reference data and adapters | Application |
| `LeavePlanner.Api` | Controller, request/response contracts, mapping, validation, exception-to-HTTP mapping, OpenAPI, composition in `Program.cs` | Application, Domain, Infrastructure at the composition root |

`tests/` has one test project per layer. Keep tests with the responsibility they exercise. The API project uses the Web SDK; if test files are ever placed under it, check compile globs to prevent the API from compiling tests.

```text
API (HTTP contracts and composition) → Application (use case and ports) → Domain (calendar and ranking)
                              API → Infrastructure (Nager and reference-data adapters) → Application
```

The request path is `LeavePlansController` → `LeavePlanningService` → `IHolidaySource`/`ISubdivisionCatalog` → `HolidayCalendarNormalizer` → `LeaveCalendar` and the selected `IRecommendationStrategy` → `RecommendationResponseMapper`. `NagerHolidayClient` implements the source port; its transport handles HTTP and its parser checks v4 data. Keep each responsibility at that boundary when extending the flow.

## Change placement

- Put calendar and ranking decisions in Domain. Its models and strategies must not use `HttpClient`, ASP.NET types, provider DTOs, configuration, or a clock.
- Put cross-layer coordination in Application: check country and subdivision support, fetch all required years, normalize records once, choose a strategy, and build a result and explanation. Declare an I/O interface here only for a genuine replaceable boundary.
- Put HTTP, status/timeout handling, JSON parsing, and bundled reference data in Infrastructure. Keep provider payload types out of Domain and public API contracts.
- Put binding, field validation, HTTP responses, and JSON mapping in API. Controllers delegate planning rather than repeat its rules. `Program.cs` and DI extensions wire implementations.

Keep normalized models immutable where practical, use `DateOnly` for calendar dates, propagate `CancellationToken` across I/O, and use structured logging for operational failures. Avoid catch-all fallbacks, null-forgiving operators that conceal bad data, duplicated validation, and duplicate ranking logic.

The current strategy extension point is `IRecommendationStrategy`. A new priority requires a deliberate contract change, strategy implementation, registration in `AddLeavePlanning`, validation, response documentation, and tests. Do not add a strategy as an incidental refactor. A different provider belongs behind `IHolidaySource`, with its own adapter and explicit DI choice. Do not add Repository, Unit of Work, CQRS/MediatR, AutoMapper, base-class frameworks, or more architecture projects without an actual need.
