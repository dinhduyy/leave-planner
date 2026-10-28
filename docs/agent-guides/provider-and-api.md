# Provider and API contract

Read this guide for Nager, HTTP, JSON, country/subdivision, validation, status-code, or API work. [README.md](../../README.md) and [LeavePlanner.http](../../LeavePlanner.http) describe the current public examples. Check the [official Nager API](https://nagerholidays.com/api) and [schema](https://nagerholidays.com/scalar/) when changing the integration; prior snapshots are not fresh verification.

## Request and data boundaries

`POST /api/leave-plans/recommendation` accepts `countryCode`, optional `subdivisionCode`, `fromDate`, `toDate`, `leaveBudget`, optional `priority`, and `minTotalDays` when required by the chosen priority. Required dates and budget use nullable request properties so omission differs from valid zero. Accept ISO alpha-2 country codes after trimming and uppercasing. Blank subdivision means absent and must produce `national-only`; reject a nonblank invalid or wrong-country subdivision. Validate inclusive date order and the 366-day limit with field-level messages. Do not depend on today's date.

The Nager v4 adapter fetches `/api/v4/Countries/Available` and `/api/v4/Holidays/{CountryCode}/{Year}` for every year in range. It uses `date`, `name`, `countryCode`, `nationalHoliday`, `subdivisionCodes`, and `holidayTypes`; do not mix v3 fields (`global`, `counties`, `types`). Confirm country support from catalog evidence, not an empty holiday list. The bundled first-level ISO subdivision catalog has its own [source note](../../LeavePlanner.Infrastructure/ReferenceData/SOURCE.md); a valid subdivision code does not guarantee the provider lists every local holiday.

Validate provider records before normalization: date and year, country, name, holiday types, and usable national/local scope metadata. Unknown additional JSON fields may be ignored; missing or contradictory fields that affect planning are bad data. A local holiday without reliable scope must not become national. Filter to applicable `Public` holidays, exclude out-of-range dates, and deduplicate in the normalizer. A valid empty list is allowed. A failed year invalidates the entire request. Keep upstream DTOs and raw responses out of API responses.

The typed `HttpClient` uses the configured HTTPS base URL and finite timeout. Do not accept an upstream URL from a request. Pass cancellation tokens through all network calls; distinguish caller cancellation from provider timeout. No unbounded retries or silent error-to-empty-list fallback.

## Public outcomes

| Condition | Response |
| --- | --- |
| Invalid input or proven unsupported country, subdivision, or year | `400` field-level `ValidationProblemDetails` |
| Valid request with no qualifying break | `200`, `recommendation: null`, actionable explanation |
| Malformed, incomplete, or contradictory provider data | `502` safe `ProblemDetails` |
| Provider unreachable, rate-limited, or failing | `503` safe `ProblemDetails` |
| Provider timeout | `504` safe `ProblemDetails` |

Do not turn caller cancellation into a `500`. Never return exception messages, stack traces, or raw upstream payloads to clients. Success must report the normalized scope, calendar assumptions, selected strategy, and the actual source. If adding fetch metadata, derive it from the real fetch rather than inventing a timestamp.
