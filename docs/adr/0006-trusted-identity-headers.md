# Demo identity through trusted headers, not real login

There is no login. The Angular role picker sends `X-User-Id` and `X-Role` headers and the API trusts them. Authorisation is real and tested: a Claimant only sees their own Claims (404 otherwise), only Officers reach Officer endpoints (403 otherwise), and only the Assigned Officer can set a Claim's Reserve, request info, approve, reject or settle it (pick-up checks only the status). With real login (JWT), Program.cs swaps the authentication scheme; `CurrentUser` keeps reading the signed-in user (it may need a claim mapping); controllers and authorisation rules stay; the Angular role picker and identity interceptor become a login flow.

## Considered Options

- **Real authentication (OIDC/JWT with an identity provider)**: rejected for the time box. It is plumbing that shows nothing about the claims domain, and it adds a container and a login flow to every demo step.
- **No identity at all**: rejected. Ownership and role checks are exactly what the panel will probe.
