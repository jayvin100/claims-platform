# Claims module

Owns the Claim, its Info Requests and Claim History, and every REST endpoint that reads or changes them (ADR-0001).

The Claim (`Claim.cs`) changes only through its public methods; each raises a domain event (`ClaimEvents.cs`) that `AppDbContext` saves to the outbox with the change.
