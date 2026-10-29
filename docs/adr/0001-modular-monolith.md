# One API as a modular monolith

The backend is one ASP.NET Core API with three modules. **Claims** owns the Claim and every REST endpoint that changes or reads it, for Claimants and Officers alike. **Notifications** and **Reporting** are Kafka consumers that only learn about Claims through events. One deployable and one database keep the platform runnable in a few commands and small enough to defend within the time box. The module seams are where it would split later: Reporting already talks to the rest only through events, so it is the first candidate to become its own service.

## Considered Options

- **A service per module (microservices)**: rejected for now. It adds deployment, networking, service discovery and cross-service data questions without changing any behaviour the brief asks for, and it would eat time better spent on the domain rules.
- **Claims / Assessment / Reporting modules** (the first draft): rejected. Claimant actions and Officer assessment both change the same Claim, so an Assessment module would split one entity's rules across two modules and make the seam fake.
- **Separate projects per module**, so the compiler enforces the boundaries: deferred. Folders in one project are enough at this size; it's listed under What was left out in the README.
