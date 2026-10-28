# Planning rules

Read this guide for calendar, holiday, budget, ranking, explanation, or product behavior changes. The current public behavior is described in [README.md](../../README.md); inspect code and tests before editing it.

## Calendar and scope

- Accept an inclusive `fromDate`–`toDate` range of at most 366 days. Past dates are valid. A recommended break must lie entirely within the range. Use `DateOnly` and day numbers; do not convert holidays through UTC or increment beyond `DateOnly.MaxValue`.
- `leaveBudget` is a non-negative cap for **one** break, not a target to spend. A larger budget than the number of workdays is valid. `minTotalDays` is a minimum qualifying break length; see the priority-specific requirement in the API contract.
- Monday–Friday are working days; Saturday–Sunday are free. A working day requires leave unless an applicable `Public` holiday covers it. A weekend holiday is still one calendar day and requires no leave.
- With a subdivision, use nationwide holidays and holidays matching that subdivision. Without one, use nationwide holidays only and state `national-only`. Do not present this as complete local coverage.
- Ignore non-`Public` types for leave calculations. Deduplicate by date while retaining distinct holiday names for explanation. Do not infer substitute days or a company-specific calendar.

For a candidate `[startDate, endDate]`, `totalDays` is the inclusive calendar length; `leaveDaysRequired` is the count of working dates without an applicable Public holiday; `nonLeaveDays = totalDays - leaveDaysRequired`. A candidate qualifies when it meets `minTotalDays`, stays inside the search range, and `leaveDaysRequired <= leaveBudget`. A holiday is not required for a valid candidate. Zero budget can produce a free-day break, or no result.

## Current ranking strategies

| Priority | Order among qualifying candidates |
| --- | --- |
| `longestBreak` (default) | More total days, fewer leave days, earlier start |
| `minimumLeave` | Fewer leave days, more total days, earlier start |
| `bestEfficiency` | A zero-leave candidate wins; among those, more total days then earlier start. Otherwise, higher `totalDays / leaveDaysRequired`, more total days, fewer leave days, earlier start |

`minimumLeave` and `bestEfficiency` require `minTotalDays`. Compare positive-leave ratios exactly; do not use floating-point rounding or divide by zero. Ranking belongs in Domain strategies. The O(D²) candidate scan is sufficient for at most 366 days; update leave counts incrementally and stop extending a start date when its budget is exceeded.

Build `leaveDates`, holiday names, and explanations from the selected calendar, in stable order. Explain the actual priority and tie breakers. Do not imply uniqueness when a tie was resolved by earlier start. The result must let a user act: dates, total days, leave days, exact leave dates, applicable holidays, scope, and assumptions. If nothing qualifies, return a clear reason and a useful adjustment suggestion without changing the request.
