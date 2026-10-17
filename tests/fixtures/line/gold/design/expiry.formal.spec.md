# Shared links expire after a chosen time

## Purpose

This specification defines an optional expiry time for shared links. A link that has no expiry time remains valid as
it is today (AC-3). Decision 0001 is reversed only for links that carry an expiry time (S1).

## Scope

In scope: the create route, the public route, the Share dialog, migration 006 and the weekly digest. Out of scope: a
change to the expiry time of an existing link, and the note's own lifetime.

## Requirements

R1. The create route shall accept an optional expiry time and shall refuse one that is not in the future (AC-4).

R2. The public route shall compare the expiry time with the server clock in UTC, once per request, and shall refuse a
request made at or after that instant (AC-2).

R3. The public route shall answer 410 with a page that states only that the link expired (AC-2, D2).

R4. A link with no expiry time, and a link made before this change, shall remain valid until revoked or until its note
is deleted (AC-3, AC-7).

R5. The Share dialog shall offer no expiry, 1 day, 7 days and 30 days, with no expiry as the default (AC-1, D1).

R6. The weekly digest shall list the owner's links that expire within 7 days (AC-6).

## Verification

| Requirement | Method | Expected result |
|---|---|---|
| R1 | API transcript | A past expiry time is refused with 400 |
| R2, R3 | API transcript | An expired link answers 410 with 0 characters of the note |
| R4 | Migration run | 0 existing rows have an expiry time |
| R5 | Browser run | Picking 7 days stores an expiry 168 hours ahead |

## Open Matters

D1 (recommended: fixed choices), D2 (recommended: the page states only the fact) and D3 (recommended: checked on read).

## Rows to Other Lanes

1. Docs: amend decision 0001 to admit an expiry time (X1).
