# Leave Planner

Leave Planner recommends one continuous break within a date range and leave budget. It tells an employee when the break starts and ends, how many calendar days it lasts, and exactly which workdays require leave. The recommendation is planning advice, not company approval.

The assignment integrates the public [Nager.Holidays Community v4 API](https://nagerholidays.com/api) and performs the calendar and ranking logic in C#. It uses the provider's raw holiday data, not a long-weekend endpoint. No database is needed: a request has no user account or history, and results are calculated from provider data retrieved directly or from the in-memory cache. The provider is configured once at startup; clients do not choose a provider per request.

## Run and try it

Requires the .NET 9 SDK and outbound HTTPS access to `nagerholidays.com`.

```powershell
dotnet restore .\LeavePlanner.sln
dotnet build .\LeavePlanner.sln --no-restore
dotnet run --project .\LeavePlanner.Api\LeavePlanner.csproj --launch-profile http
```

Open [LeavePlanner.http](LeavePlanner.http) in an HTTP client and run its first request against `http://localhost:5257`. It uses Austria in 2026, a year supported by the provider when this project was prepared. Live holiday data may change. The OpenAPI document is available at `/openapi/v1.json` in Development.

Live smoke test on 2026-10-27: that request returned HTTP 200 with a five-day break (2026-05-01 through 2026-05-05), using leave on 2026-05-04 and 2026-05-05. The response depends on current provider data.

Run the network-independent test suite with:

```powershell
dotnet test .\LeavePlanner.sln
```

The optional live smoke test is to run the application and send the first `.http` request. It is separate from automated tests.

`HolidayProvider:BaseUrl` and `HolidayProvider:TimeoutSeconds` are configured in `LeavePlanner.Api/appsettings.json`; the defaults are the HTTPS Nager endpoint and ten seconds. Do not set the base URL from a request.

`HolidayCache:DurationHours` defaults to `24` and `HolidayCache:MaxEntries` to `512` in the same file. Duration must be 1–8760 hours and the entry limit must be positive; invalid settings fail at startup. The cache stores the validated country catalog and each country's yearly holiday list, including valid empty lists, for an absolute lifetime measured from a successful load. It is private to each application process and clears on restart. Data can be up to the configured lifetime old; different processes may refresh at different times. Once an entry expires, an upstream failure produces the normal provider error rather than serving stale data. The entry limit counts cached responses, not bytes.

## API

`POST /api/leave-plans/recommendation`

| Request field | Meaning |
| --- | --- |
| `countryCode` | Required ISO alpha-2 country code supported by Nager. Spaces are trimmed and letters uppercased. |
| `subdivisionCode` | Optional first-level ISO 3166-2 code, such as `AT-9`. Blank becomes absent. |
| `fromDate`, `toDate` | Required inclusive `YYYY-MM-DD` dates, ordered, at most 366 days apart inclusively. |
| `leaveBudget` | Required non-negative integer maximum for this one break; zero is valid. |
| `priority` | Optional `PlanningPriority` enum: `longestBreak` (default), `minimumLeave`, or `bestEfficiency` in JSON. Numeric values are rejected. |
| `minTotalDays` | Required for `minimumLeave` and `bestEfficiency`; optional for `longestBreak`. Must be 1 through the search range length. |

All priorities use the same eligibility rules: the break stays within the search range, meets `minTotalDays`, and needs no more than `leaveBudget` days. A Monday–Friday day requires leave unless an applicable `Public` holiday covers it. Saturday and Sunday do not require leave. A holiday on a weekend is counted once. Other holiday types do not reduce leave.

Ranking is deterministic:

1. `longestBreak`: more total days, then fewer leave days, then earlier start.
2. `minimumLeave`: fewer leave days, then more total days, then earlier start.
3. `bestEfficiency`: if an eligible zero-leave break exists, select the longest such break, then earlier start. Otherwise maximize `totalDays / leaveDaysRequired`; ties favor more total days, fewer leave days, then earlier start. The implementation compares ratios exactly, without division by zero.

**Synthetic strategy example, not an Austrian holiday result:** Given A = 9 days/4 leave, B = 7/2, C = 5/1, D = 2/0 and budget 4, `longestBreak` selects A. `minimumLeave` with `minTotalDays = 7` selects B. `bestEfficiency` with `minTotalDays = 3` selects C; D is too short. A zero-leave option that does meet the minimum would take precedence for `bestEfficiency`.

The response echoes normalized scope, budget, priority and calendar policy. `recommendation` contains `startDate`, `endDate`, `totalDays`, `leaveDaysRequired`, `nonLeaveDays`, ordered `leaveDates`, and applicable `publicHolidays` with names. `explanation` gives the selection reason or why none qualifies. `assumptions` and `holidaySource` state the calendar and data limits.

**Synthetic response fragment from a test fixture, not verified live AT data:**

```json
{
  "countryCode": "AT",
  "priority": "longestBreak",
  "calendar": { "workingDays": "Monday-Friday", "weekendDays": "Saturday-Sunday", "holidayType": "Public", "scope": "national-only" },
  "recommendation": {
    "startDate": "2026-05-01",
    "endDate": "2026-05-01",
    "totalDays": 1,
    "leaveDaysRequired": 0,
    "nonLeaveDays": 1,
    "leaveDates": [],
    "publicHolidays": [{ "date": "2026-05-01", "names": ["Synthetic Public Holiday"] }]
  }
}
```

If no break qualifies, HTTP 200 has `recommendation: null` and an explanation suggesting a larger budget or adjusted range/minimum. Input errors and proven unsupported country, subdivision or year return HTTP 400 ValidationProblemDetails with field messages. Malformed or incomplete provider data returns 502, provider connection/rate-limit/service errors return 503, and upstream timeouts return 504. A failed year in a multi-year request never yields a partial recommendation. Errors do not include raw provider responses or stack traces.

## Data and limits

The service retrieves every year in the search range from Nager v4 or the in-memory cache, checks the country against `/api/v4/Countries/Available`, and uses `date`, `name`, `countryCode`, `nationalHoliday`, `subdivisionCodes`, and `holidayTypes`. An empty valid holiday list is different from an unsupported country. Nager v4 supports only a provider-defined window of years; years outside it are rejected with a field error. Past dates are accepted when their year is supported.

Without a subdivision, only nationwide holidays apply and the response says `national-only`. With a subdivision, nationwide and matching regional holidays apply. The ISO 3166-2 first-level code catalog is bundled from a pinned [Ipregistry snapshot](LeavePlanner.Infrastructure/ReferenceData/SOURCE.md), because Nager v4 does not publish a subdivision list. A valid ISO code does not guarantee complete local holiday coverage; the response describes only holidays listed by the provider. No company-specific calendar, half days, shift work, substitute-day policy, leave balance, or multiple-break planning is included.

## Code structure

```text
LeavePlanner.sln
├── LeavePlanner.Api/
├── LeavePlanner.Application/
├── LeavePlanner.Domain/
├── LeavePlanner.Infrastructure/
└── tests/
    ├── LeavePlanner.Api.Tests/
    ├── LeavePlanner.Application.Tests/
    ├── LeavePlanner.Domain.Tests/
    └── LeavePlanner.Infrastructure.Tests/
```

`Domain` owns the pure `LeaveCalendar`, candidate enumeration in `LeavePlannerOptimizer`, and three ranking Strategies. Each Strategy carries a `PlanningPriority` enum value, minimum-length requirement and selection reason. `Application` depends on Domain; it holds `IHolidaySource` and `ISubdivisionCatalog`, resolves Strategies through a registry, coordinates yearly fetches, normalizes holidays and builds the business explanation. `Infrastructure` depends on Application: its Nager adapter separates HTTP/status/timeout handling from v4 response parsing, a cache decorator reuses validated provider data, and its reference-data adapter holds the subdivision catalog. `LeavePlanner.Api` composes the layers and owns JSON contracts, request validation, response mapping, and controllers. Adding a Strategy requires implementing `IRecommendationStrategy` and registering it explicitly in `AddLeavePlanning`; adding a provider requires an `IHolidaySource` adapter and changing startup infrastructure registration. Tests use synthetic data and fake HTTP handlers, so they do not depend on live provider changes.

Request fields use Data Annotations; cross-field date and priority rules use `IValidatableObject`. ASP.NET Core's `[ApiController]` returns field-level `ValidationProblemDetails` before the controller runs. Provider-supported country and subdivision checks remain in the planning service.

Inside `LeavePlanner.Domain`, `Calendar` defines applicable holidays and workdays, `Planning` enumerates eligible breaks, and `Strategies` ranks them. The four application projects are siblings beside the solution; all four test projects live under `tests/` and share xUnit settings through `tests/Directory.Build.props`. Each test project references only the layers its tests use. See the [edge-case matrix](tests/EDGE_CASES.md) for the coverage map.
