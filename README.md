# Rezerv Booking Engine (.NET)

A simplified booking engine for a fitness studio platform. Customers buy credit packages from a business and spend credits to book that business's timetable schedules. Full classes have a FIFO waitlist, and concurrent bookings are handled safely.

| | |
|---|---|
| Runtime | .NET 10, ASP.NET Core Web API |
| Database | MySQL 8 via EF Core 9 (Pomelo) |
| Cache / locks | Redis 7 via StackExchange.Redis |
| Docs | Swagger UI at `http://localhost:8080/` |
| Background jobs | Hangfire (dashboard at `/hangfire`) |
| Tests | xUnit + EF InMemory |

---

## 1. Setup

### Option A - everything in Docker

```bash
docker compose up --build
```

Open `http://localhost:8080/` for Swagger UI. The API creates the schema and seeds sample data on first start.

### Option B - API locally, infrastructure in Docker

```bash
docker compose up mysql redis -d
dotnet run --project src/Rezerv.API
```

Connection strings are in `src/Rezerv.API/appsettings.json`.

### Tests

```bash
dotnet test
```

Tests use EF's in-memory provider and an in-process lock, so MySQL and Redis are not required.

### Reset data

```bash
docker compose down -v && docker compose up --build
```

Notes: the solution file is `Rezerv.slnx` (new format), which requires .NET 10 SDK, Visual Studio 2022 17.13+ or Rider. EF Core is pinned to 9.x because Pomelo does not yet ship a stable EF Core 10 provider.

---

## 2. Architecture

Clean architecture with four projects, kept intentionally small (no MediatR / CQRS).

```
src/
  Rezerv.Domain          Entities and enums. No dependencies.
                         Small invariants live here (UserPackage.DeductCredit / RefundCredit,
                         TimetableSchedule.OverlapsWith / HasStarted / HasEnded).
  Rezerv.Application     Services (BookingService, WaitlistService, PackageService, TimetableService),
                         BookingRules (validations shared by booking and waitlist promotion),
                         DTOs, BusinessRuleException, and the interfaces that Infrastructure implements
                         (IApplicationDbContext, IDistributedLockService, ICacheService, IClock).
  Rezerv.Infrastructure  EF Core DbContext and mapping (MySQL), Redis lock and cache, DbSeeder, DI registration.
  Rezerv.API             Thin controllers, error middleware, Swagger, Hangfire, Program.cs.
tests/Rezerv.Tests       Business rule, waitlist and concurrency tests.
docs/                    erd.md (Mermaid ERD) and schema.sql.
```

Key decisions

- Services throw `BusinessRuleException(statusCode, code, message)`. One middleware turns it into `{ code, message }` with the right HTTP status, so controllers are one line each and every failure has a stable machine-readable code.
- `BookingRules` holds the validations used by both booking and waitlist promotion, so the two paths cannot drift apart.
- `IClock` is injected wherever time matters, which makes the 4-hour refund rule and "class ended" logic deterministic in tests.
- Attendance count is derived, never stored: `attendanceCount = COUNT(bookings WHERE status = Booked)`. A stored counter is the classic source of overbooking bugs.

Request flow for `POST /api/bookings`:

```
Controller -> BookingService.BookAsync
  1. acquire Redis lock  lock:schedule:{id}
  2. load schedule, wallet, user's active bookings, booked count
  3. BookingRules.EnsureCanBook
       not started -> wallet belongs to user -> not expired -> same business
       -> at least 1 credit -> not already booked -> no overlapping booking -> slots left
  4. wallet.DeductCredit() + insert booking, saved in one SaveChanges (one transaction)
  5. remove timetable cache key
  6. release lock (finally; only if the token is still ours)
```

---

## 3. API

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/packages?businessId=` | Packages available for purchase |
| POST | `/api/packages/purchase` | Mock purchase, creates a credit wallet for the user |
| GET | `/api/timetable?businessId=&date=yyyy-MM-dd` | Schedules with attendance and availability (Redis cached) |
| POST | `/api/bookings` | Book a schedule, deducts 1 credit |
| POST | `/api/bookings/cancel` | Cancel; refund if more than 4h before start; promotes first waiter |
| GET | `/api/bookings?userId=` | User's bookings |
| POST | `/api/waitlist` | Join FIFO waitlist of a full schedule |
| GET | `/api/waitlist/{scheduleId}` | Queue with positions |
| POST | `/api/waitlist/expire-ended` | Run the "class ended" cleanup now (Hangfire also runs it every 5 min) |

Error envelope: `{ "code": "SCHEDULE_FULL", "message": "..." }`

| HTTP | Codes |
|---|---|
| 400 | `PACKAGE_EXPIRED` `INSUFFICIENT_CREDITS` `BUSINESS_MISMATCH` `OVERLAPPING_BOOKING` `SCHEDULE_ALREADY_STARTED` `SCHEDULE_ALREADY_ENDED` `PACKAGE_NOT_OWNED` `BOOKING_NOT_OWNED` |
| 404 | `SCHEDULE_NOT_FOUND` `PACKAGE_NOT_FOUND` `USER_NOT_FOUND` `BOOKING_NOT_FOUND` |
| 409 | `SCHEDULE_FULL` `ALREADY_BOOKED` `ALREADY_WAITLISTED` `SCHEDULE_NOT_FULL` `BOOKING_NOT_ACTIVE` `CONCURRENCY_CONFLICT` |
| 503 | `LOCK_TIMEOUT` |

---

## 4. Concurrency strategy

Problem: 5 slots, 10 users click "book" at once. "Count bookings, compare, insert" is a read-then-write race; two requests can both see 4/5 and both insert.

Three layers, each covering a failure mode of the one above:

| Layer | Mechanism | Protects against |
|---|---|---|
| 1. Redis distributed lock per schedule | `SET lock:schedule:{id} token NX PX 10000` (StackExchange `LockTake`), polled every 50 ms up to 5 s. Release deletes the key only if it still holds our token. | The read-then-write race. All writers of one schedule, across every API instance, run one at a time. Different schedules do not contend. |
| 2. Single SaveChanges / DB transaction | Credit deduction and booking insert commit together | Partial writes (credit taken but no booking, or the reverse) |
| 3. Optimistic concurrency token on `RemainingCredits` | EF emits `UPDATE ... WHERE RemainingCredits = @read`; a mismatch throws `DbUpdateConcurrencyException`, mapped to 409 | Double-spend of a credit even if the lock is lost (Redis restart, TTL expiring under a slow request) |

The same lock wraps cancel and join-waitlist, so "cancel + promote the first waiter" is atomic with respect to new bookings on that schedule and FIFO order cannot be jumped.

Why not only `SELECT ... FOR UPDATE`? It would work for a single database and is simpler, but the brief requires Redis, and the Redis lock keeps contention out of MySQL. Layer 3 is kept because a Redis lock alone is not a correctness guarantee.

Known limits

- Single-node Redis `SET NX` is not Redlock. During a Redis failover a lock could briefly be held twice. The DB token still prevents credit double-spend; at worst one extra attendee could be admitted during the failover window. Production hardening: RedLock.net over several nodes, or move the slot check into the database.
- Lock TTL (10 s) must exceed the longest critical section. Requests are a few queries, so this is generous.
- Waiting requests give up after 5 s with `503 LOCK_TIMEOUT` so latency is bounded.

`tests/Rezerv.Tests/ConcurrencyTests.cs` reproduces the 10-users / 5-slots scenario with one DbContext per request and asserts exactly 5 bookings and 5 credits spent.

---

## 5. Caching strategy

`GET /api/timetable` is the read-heavy endpoint. The full timetable is cached under one key (`timetable:all`) for 30 seconds, and filters (business, date) are applied in memory. Every write path (book, cancel, join waitlist, expiry) removes the key.

The cache is fail-open: if Redis is down, reads go to MySQL and the API keeps working. Only the lock is mandatory for writes.

Tradeoff: one key is simple and always correct for an assessment-size dataset. With thousands of schedules I would cache per business/date and invalidate with a version key (`timetable:v{N}:...`, bumped with `INCR`) so no `KEYS`/`SCAN` is needed.

---

## 6. Assumptions

1. No authentication. `userId` is sent in request bodies / query strings. In production it would come from the auth token.
2. A booking spends from an explicit `userPackageId`. The server validates that the wallet belongs to the user, is unexpired and matches the schedule's business.
3. Joining the waitlist does not deduct or hold a credit. The user must be eligible at join time (valid package, at least 1 credit, same business, no overlap), but the credit is only deducted when promoted. "Release the reservation" on class end is therefore a no-op by construction.
4. Promotion re-validates. If the first waiter is no longer eligible (expired package, 0 credits, overlapping booking), the entry is marked `Skipped` with the reason and the next waiter is tried.
5. Promotion happens only before class start. A cancellation after start frees nobody.
6. Refund rule is strict: refund only if `cancelledAt < startTime - 4h`. Cancelling at exactly 4 hours before is "within 4 hours", no refund.
7. Cancelling after the class ended is rejected (`SCHEDULE_ALREADY_ENDED`). Cancelling between start and end is allowed with no refund.
8. Bookings are rejected for classes that already started.
9. Overlap = `A.start < B.end && B.start < A.end` across all businesses. Back-to-back classes (10:00-11:00 and 11:00-12:00) do not overlap.
10. One active booking per user per schedule, one active waitlist entry per user per schedule.
11. All times are UTC.
12. Package purchase is a mock: no payment, credits granted immediately, `expiresAt = now + validityDays`.
13. Schema is created with `EnsureCreated()` on startup for one-command setup. A real project would use EF migrations; `docs/schema.sql` documents the resulting schema.
14. Hangfire uses in-memory storage to keep the assessment self-contained. The expiry job is idempotent, so a missed run is harmless.
15. EF Core is pinned to 9.x on .NET 10 because Pomelo has no stable EF Core 10 provider yet.

---

## 7. Sample data and test matrix

Seeded on first start: 2 businesses, 3 packages, 12 users, 12 wallets (`userPackageId` = `userId`), 10 schedules, 13 bookings, 5 waitlist entries.

| Schedule | Business | Class | Capacity | Scenario |
|---|---|---|---|---|
| 1 | 1 Rezerv Fitness | Yoga Class, +2d 10:00 | 2/15 | open |
| 2 | 1 | HIIT Blast, +2d 10:30 | 2/10 | overlaps schedule 1 |
| 3 | 1 | Spin Class, +2d 18:00 | 2/2 | full, waitlist: user 8 then user 9 |
| 4 | 1 | Pilates, now + 2h | 1/5 | late cancel (no refund) |
| 5 | 1 | Boxing Basics, now + 6h | 1/5 | early cancel (refund), concurrency target |
| 6 | 1 | Strength 101, +3d 09:00 | 1/1 | full; first waiter (user 2) has 0 credits |
| 7 | 1 | Morning Yoga, yesterday | 1/1 | ended; user 8 still waiting |
| 8 | 2 Zen Yoga Studio | Hatha Yoga, +2d 08:00 | 0/8 | open |
| 9 | 2 | Meditation, +2d 12:00 | 3/3 | full, no waitlist |
| 10 | 2 | Vinyasa Flow, +4d 17:00 | 0/12 | open |

Wallets: user 2 has 0 credits, user 3's wallet is expired, user 4 holds a business-2 wallet, everyone else holds a valid business-1 wallet.

| # | Edge case | Request | Expect |
|---|---|---|---|
| 1 | Happy path | `POST /api/bookings` `{"userId":4,"scheduleId":8,"userPackageId":4}` | 201, `remainingCredits: 3` |
| 2 | Expired package | `POST /api/bookings` `{"userId":3,"scheduleId":5,"userPackageId":3}` | 400 `PACKAGE_EXPIRED` |
| 3 | Insufficient credits | `POST /api/bookings` `{"userId":2,"scheduleId":5,"userPackageId":2}` | 400 `INSUFFICIENT_CREDITS` |
| 4 | Wrong business | `POST /api/bookings` `{"userId":1,"scheduleId":8,"userPackageId":1}` | 400 `BUSINESS_MISMATCH` |
| 5 | Overlapping schedule | `POST /api/bookings` `{"userId":5,"scheduleId":2,"userPackageId":5}` | 400 `OVERLAPPING_BOOKING` |
| 6 | Full schedule | `POST /api/bookings` `{"userId":10,"scheduleId":3,"userPackageId":10}` | 409 `SCHEDULE_FULL` |
| 7 | Join waitlist | `POST /api/waitlist` `{"userId":10,"scheduleId":3,"userPackageId":10}` | 201, `position: 3`, credits unchanged |
| 8 | Waitlist on non-full class | `POST /api/waitlist` `{"userId":4,"scheduleId":10,"userPackageId":4}` | 409 `SCHEDULE_NOT_FULL` |
| 9 | Waitlist promotion (FIFO) | `POST /api/bookings/cancel` `{"userId":6,"bookingId":5}` | 200, `creditRefunded: true`, `promotedUserId: 8` |
| 10 | Promotion skips ineligible | `POST /api/bookings/cancel` `{"userId":6,"bookingId":9}` | `promotedUserId: 10`; `GET /api/waitlist/6` shows user 2 `Skipped` |
| 11 | Late cancellation | `POST /api/bookings/cancel` `{"userId":1,"bookingId":7}` | `creditRefunded: false` |
| 12 | Early cancellation | `POST /api/bookings/cancel` `{"userId":1,"bookingId":8}` | `creditRefunded: true` |
| 13 | Class already started | `POST /api/bookings` `{"userId":5,"scheduleId":7,"userPackageId":5}` | 400 `SCHEDULE_ALREADY_STARTED` |
| 14 | Waitlist after class ends | `POST /api/waitlist/expire-ended` then `GET /api/waitlist/7` | `expired: 1`, entry `Expired`, user 8's credits untouched |
| 15 | Concurrent bookings | script below against schedule 5 (4 free slots) | exactly 4 x 201, rest 409 `SCHEDULE_FULL` |

Concurrency script (run before #12):

```bash
for u in 6 7 8 9 10 11 12; do
  curl -s -o /dev/null -w "user $u -> %{http_code}\n" -X POST http://localhost:8080/api/bookings \
    -H 'Content-Type: application/json' \
    -d "{\"userId\":$u,\"scheduleId\":5,\"userPackageId\":$u}" &
done; wait
curl -s http://localhost:8080/api/timetable | python3 -m json.tool | grep -A2 '"scheduleId": 5'
```

Ids above are those produced by the seeder on a fresh database. If in doubt, confirm booking ids with `GET /api/bookings?userId=...`.

---

## 8. Tradeoffs considered

| Decision | Alternative | Why this one |
|---|---|---|
| Derived attendance count | `bookedCount` column on schedule | A counter must be kept consistent under every write path; the derived count is correct by construction and the `(ScheduleId, Status)` index keeps it cheap. |
| Credit deducted at promotion, nothing held while waiting | Reserve a credit on join | Holding credits blocks customers from booking other classes for an uncertain outcome. Cost: a waiter can become ineligible, handled by `Skipped`. |
| Redis lock + DB concurrency token | Only `SELECT ... FOR UPDATE` | Brief requires Redis; keeps hot-class contention off MySQL; the DB token remains the money-safety net. |
| Single cache key, filter in memory | Per-filter keys with version invalidation | Simpler and always correct at this data size; the version-key approach is documented as the next step. |
| `BusinessRuleException` + one middleware | `Result<T>` return type | Keeps service code linear and controllers trivial; HTTP mapping lives in one place. |
| `IApplicationDbContext` in Application | Repository + Unit of Work | EF already is a repository/UoW; an extra layer adds code without adding testability. |
| Plain DTO classes | C# records | Consistency with the rest of the codebase; behaviour is identical for serialization. |
| `EnsureCreated` + seeder | EF migrations | One-command setup for reviewers; migrations noted as the production path. |

---

## 9. How this could scale in production

- Stateless API: every instance shares MySQL and Redis, so horizontal scaling is a replica-count change.
- Lock granularity is per schedule, so popular classes do not block each other. For a single very hot class, replace the lock with a Redis-side atomic reservation (Lua `if count < slots then INCR`) and write the booking asynchronously.
- Reads: move `/api/timetable` to a read replica, cache per business/date with a version key, raise TTL.
- Waitlist promotion: publish `BookingCancelled` to a queue (Redis Streams / RabbitMQ / SQS) and let a worker promote with retries, so cancel latency is independent of queue length and notifications hang off the same event.
- Hangfire: swap in-memory storage for MySQL or Redis storage so jobs survive restarts and run once across instances.
- Observability: structured logs with user/schedule/package ids; OpenTelemetry traces around lock wait time and `CONCURRENCY_CONFLICT` counters.
- Data growth: partition `Bookings` by month, archive cancelled/ended rows.
- Idempotency: `Idempotency-Key` header on `POST /api/bookings` stored in Redis so client retries after a timeout do not double-book.

---

## 10. With more time

- EF migrations instead of `EnsureCreated`; integration tests with Testcontainers (real MySQL + Redis) exercising the Redis lock itself.
- Auto-pick the best wallet when `userPackageId` is omitted.
- `DELETE /api/waitlist/{id}` (leave the waitlist) and paged booking history.
- Notifications on promotion, idempotency keys, rate limiting.
