# 08: Angular shell and role picker

**What to build:** A user opens the Angular app, picks a seeded Claimant or Officer, and lands on the screens for that role. The choice is remembered until they switch, and switching user switches screens. Every API call carries the chosen user's identity headers.

**Blocked by:** 01: Runnable skeleton with demo identity

**Status:** ready-for-agent

- [ ] Angular app (latest stable) created with the Angular CLI through `npx`; standalone components, signals for state, plain CSS; no npm packages beyond what `ng new` installs
- [ ] The role picker lists users from `GET /api/users`
- [ ] The chosen user is remembered across reloads until the user switches
- [ ] One HTTP interceptor adds `X-User-Id` and `X-Role` to every API call
- [ ] A route guard per role keeps Claimants off Officer routes and the reverse
- [ ] A visible "switch user" control on every screen
- [ ] The API allows the Angular dev server's origin (CORS) in Development
