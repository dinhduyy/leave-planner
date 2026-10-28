# Verification and delivery

Read this guide for tests, edge cases, verification, completion, or demo work. [tests/EDGE_CASES.md](../../tests/EDGE_CASES.md) is the coverage map and prior verification record; it does not prove a new change passed.

## Test the affected boundary

Before implementing a behavior change, list relevant conditions with input, expected outcome, and owning layer. Add meaningful tests with synthetic, clearly labeled data. Keep automated tests independent of Nager and the network.

| Change | Test focus |
| --- | --- |
| Calendar or strategy | Inclusive length, budget, all tie breakers, zero and excess budget, no holidays, leap days, one-day range, `DateOnly` extremes, stable ordered leave dates and explanation |
| Holiday normalization | Duplicate/out-of-order records, names sharing a date, weekend overlap, national/local/wrong-region scope, blank subdivision, non-`Public` types, out-of-range records |
| Provider client | v4 fixture, empty valid list, null/malformed body, missing or conflicting fields, wrong country/year, unknown types, non-success status, timeout and caller cancellation |
| Application/API | Missing/null/malformed body, wrong types and missing fields, normalization, date limits, support checks, no result, a failed year in a multi-year request, assumptions, concurrent requests |

Verify invariants on varied synthetic calendars: inclusive `totalDays = leaveDaysRequired + nonLeaveDays`; `leaveDaysRequired = leaveDates.Count`; leave dates are unique, ordered, in range, and genuinely require leave; the selected break fits the request and budget; reported holidays apply to the selected scope. For the same input and calendar, record order and duplicate holidays must not change the selection. With the same range and calendar, increasing budget must not reduce the best `longestBreak` length. Keep strategy-specific properties separate; do not impose longest-break monotonicity on another priority.

## Checks and reporting

For implementation work, run `dotnet restore .\LeavePlanner.sln`, `dotnet build .\LeavePlanner.sln --no-restore`, and `dotnet test .\LeavePlanner.sln --no-build` when SDK and packages are available. The four test projects should all run. A live smoke request is optional and separate; never make network data a unit-test prerequisite. If an SDK, package, network, or command fails, state exactly what was not verified.

For documentation-only work, check changed text, relative links, and consistency with current code and README; a build is unnecessary. Before calling an implementation complete, confirm that the main path uses real provider HTTP data, the output gives actionable leave dates and a faithful explanation, and README plus `.http` let a reviewer run it. Report concrete checks and remaining limits. Do not describe a synthetic country/year example as verified live data.
