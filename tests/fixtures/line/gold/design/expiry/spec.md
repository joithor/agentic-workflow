# Shared links expire after a chosen time

## Summary

A member who shares a note can pick when the link stops working. A link with no chosen time works as it does today
(AC-3). An expired link answers with a page that says it expired and never returns the note (AC-2, AC-5). This
reverses decision 0001 only for links that choose a time (S1).

## Behaviour

### Creating a link

The create route (S2, S5) takes an optional expiry time. It refuses a time that is not in the future and creates no
link (AC-4). Any editor may set one; a viewer still cannot create a link (AC-10).

### Opening a link

assumes: D3.a

The public route (S3, S6) compares the link's expiry time with the server clock, in UTC, once per request. The
instant of expiry is the end: a request at that instant is refused (AC-2). A revoked link and an expired link both end
the link, and expiry is not behind the linkRevocation flag (AC-5, S12). A link with no expiry time stays valid until
it is revoked or its note is deleted (AC-3). A link made before this change counts as having none (AC-7).

### The expired page

assumes: D2.a

The public route answers 410 with a page that says the link expired, and nothing else. It does not say whether the
note still exists (S23, AC-2, D2).

### The picker

assumes: D1.a

The Share dialog (S4, S22) offers four choices: no expiry, 1 day, 7 days and 30 days. No expiry is the default
(AC-1, D1).

### The digest notice

The weekly digest lists the owner's links that expire within 7 days, one line for each (S17, S18, AC-6). A week with
no changed note and no expiring link sends nothing (AC-9).

## Data

assumes: D3.a

Migration 006 adds a nullable `expires_at` column to `shared_links`. It holds a UTC instant in ISO 8601 with a `Z`
suffix. Old rows stay null (AC-7). The public route reads the column on each request, and no job writes it (D3).

## Proof

Each item has a signal in `signals.json`. Create, refuse and open are shown with API transcripts at the head. The
picker is shown in a browser. Migration 006 is shown on a database that already holds two links (AC-7). The
regressions are compared at the base and at the head: revoke (AC-8), the quiet digest (AC-9), the viewer refusal
(AC-10) and the export (AC-11, an added item under sec-4).

## Rows to other lanes

1. Docs: edit decision 0001 so that a link may now choose an expiry time (X1).
