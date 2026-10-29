# Demo identity through trusted headers, not real login

There is no login. The Angular role picker sends `X-User-Id` and `X-Role` headers and the API trusts them. Authorisation is real and tested: a Claimant only sees their own Claims (404 otherwise), only Officers reach Officer endpoints (403 otherwise), and only the Assigned Officer can act on a Claim. In production, the same checks would read the user id and role from a validated JWT instead of headers; nothing else changes.

## Considered Options

- **Real authentication (OIDC/JWT with an identity provider)**: rejected for the time box. It is plumbing that shows nothing about the claims domain, and it adds a container and a login flow to every demo step.
- **No identity at all**: rejected. Ownership and role checks are exactly what the panel will probe.
