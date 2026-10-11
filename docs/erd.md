# Entity Relationship Diagram

```mermaid
erDiagram
    Business ||--o{ Package : offers
    Business ||--o{ TimetableSchedule : runs
    User ||--o{ UserPackage : owns
    Package ||--o{ UserPackage : "purchased as"
    UserPackage ||--o{ Booking : "pays for"
    UserPackage ||--o{ WaitlistEntry : "will pay for"
    TimetableSchedule ||--o{ Booking : has
    TimetableSchedule ||--o{ WaitlistEntry : has
    User ||--o{ Booking : makes
    User ||--o{ WaitlistEntry : joins

    Business {
        int Id PK
        string Name
    }
    User {
        int Id PK
        string Name
        string Email
    }
    Package {
        int Id PK
        int BusinessId FK
        string Name
        int TotalCredits
        int ValidityDays
        decimal Price
    }
    UserPackage {
        int Id PK
        int UserId FK
        int PackageId FK
        int BusinessId
        int TotalCredits
        int RemainingCredits "concurrency token"
        datetime PurchasedAt
        datetime ExpiresAt
    }
    TimetableSchedule {
        int Id PK
        int BusinessId FK
        string ClassName
        string InstructorName
        datetime StartTime
        datetime EndTime
        int Capacity
    }
    Booking {
        int Id PK
        int UserId FK
        int ScheduleId FK
        int UserPackageId FK
        enum Status "Booked | Cancelled"
        datetime BookedAt
        datetime CancelledAt
        bool CreditRefunded
    }
    WaitlistEntry {
        int Id PK
        int UserId FK
        int ScheduleId FK
        int UserPackageId FK
        enum Status "Waiting | Promoted | Skipped | Expired"
        datetime JoinedAt
        datetime ResolvedAt
        string Note
    }
```

## Indexes

- Booking (ScheduleId, Status) - attendance count per class
- Booking (UserId, Status) - overlap check per user
- WaitlistEntry (ScheduleId, Status, JoinedAt) - FIFO queue
- UserPackage (UserId, BusinessId) - wallet lookup

## Notes

- Attendance count is never stored; it is always `COUNT(Booking WHERE Status = Booked)`.
- `UserPackage.RemainingCredits` is an EF Core concurrency token (optimistic concurrency).
