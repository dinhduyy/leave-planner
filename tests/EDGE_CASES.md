# Leave Planner edge-case matrix

All named holidays and countries in automated tests are synthetic unless a fixture says otherwise. The responsible layer is listed before implementing each flow.

| Input or condition | Expected result | Layer |
| --- | --- | --- |
| Null/missing body, malformed JSON, missing fields, invalid date or budget type | 400 ValidationProblemDetails with field | ASP.NET binding / request validation |
| Blank country, invalid alpha-2, negative budget, reversed dates, span >366, invalid priority/minimum | 400 with corrective field message | Request validation |
| Lowercase/padded country and blank subdivision | Normalize; blank subdivision becomes national-only | Request validation |
| ISO subdivision not in country or child-level code | 400 on subdivisionCode | Subdivision catalog / planning service |
| Supported country with empty holiday list | Continue with weekends/leave; no unsupported inference | Country metadata / planning service |
| Provider rejects a year with explicit year evidence | 400 naming year; no partial recommendation | Nager client / API error mapping |
| One year fails in multi-year query | Error, never a partial success | Planning service |
| Public national/local holiday, wrong region, non-Public type | Only applicable Public days lower leave count | Calendar normalizer |
| Duplicate day/names, weekend overlap, unordered and out-of-range records | One non-leave day per date; stable ordered explanations | Calendar normalizer |
| Missing or inconsistent upstream scope/date/country/type | 502, not an empty calendar | Nager client |
| Provider unreachable/rate limited, timeout, caller cancellation | 503, 504, cancellation propagated respectively | Nager client / API error mapping |
| Budget 0, no candidate meeting minimum | 200 with null recommendation and helpful reason | Optimizer / planning service |
| DateOnly extrema, leap day, inclusive 366-day range | No overflow; correct inclusive counts | Request validation / optimizer |
| Concurrent requests | No calendar or strategy state leaked between them | Planning service / API tests |

## Verification record — 2026-10-28

| Flow | Tests |
| --- | --- |
| Binding, normalization, required fields, date span, priority and minimum | `LeavePlanner.Api.Tests/RecommendationRequestTests.cs`, `LeavePlanner.Api.Tests/RecommendationEndpointTests.cs` |
| Calendar scope, duplicates, `Public` filtering and stable order | `LeavePlanner.Application.Tests/HolidayCalendarNormalizerTests.cs`, `LeavePlanner.Infrastructure.Tests/IsoSubdivisionCatalogTests.cs` |
| Budget, three Strategy rankings, zero-leave behavior, leap year, `DateOnly` extrema, and calendar extraction | `LeavePlanner.Domain.Tests/RecommendationStrategyTests.cs`, `LeavePlanner.Domain.Tests/LeavePlannerOptimizerTests.cs`, `LeavePlanner.Domain.Tests/LeaveCalendarTests.cs` |
| Strategy enum/default lookup and duplicate registration | `LeavePlanner.Application.Tests/RecommendationStrategyRegistryTests.cs` |
| Nager v4 schema, empty lists, malformed scope, status, timeout and cancellation | `LeavePlanner.Infrastructure.Tests/NagerHolidayClientTests.cs` |
| Country/subdivision support, multi-year failure, no result and concurrent API calls | `LeavePlanner.Application.Tests/LeavePlanningServiceTests.cs`, `LeavePlanner.Api.Tests/RecommendationEndpointTests.cs` |

`dotnet restore LeavePlanner.sln`, `dotnet build LeavePlanner.sln --no-restore` (0 warnings/errors), and `dotnet test LeavePlanner.sln --no-build` (113 passed, 0 failed across four test projects) completed successfully after the sibling-project layout change. A separate live smoke request on 2026-10-27 to the local API using the first `LeavePlanner.http` request and Nager AT/2026 returned HTTP 200 with a five-day recommendation. Live provider data can change independently of the automated fixtures.
