# Claims Platform

Motor and property claims across six APAC markets. Claimants report and track claims; Officers assess them through to settlement or rejection.

## People

**Claimant**:
The person who reports an incident and owns the resulting Claim.
_Avoid_: Customer, policyholder, user

**Officer**:
A member of claims staff who picks up, assesses and decides Claims.
_Avoid_: Adjuster, handler, agent, staff member

**Assigned Officer**:
The Officer who picked up a Claim. Only they can act on it from then on.
_Avoid_: Owner, handler

**Demo User**:
One of the seeded Claimants or Officers the role picker can act as. There is no login; the chosen Demo User's id and role travel as `X-User-Id` and `X-Role`. In code, "user" means a Demo User.
_Avoid_: Account, login

**Team**:
All Officers, taken together. There is one Team.
_Avoid_: Department, group

## Claims

**Claim**:
A Claimant's request to be paid for a motor or property incident under a policy.
_Avoid_: Case, ticket, request

**Claim Number**:
The readable reference a Claimant quotes, made of the Market code and a sequence number.
_Avoid_: Reference, case number

**Claim Type**:
Whether a Claim is Motor or Property.

**Policy Number**:
The Claimant's policy reference, as typed by the Claimant. It is not verified.
_Avoid_: Policy (there is no Policy entity)

**Market**:
One of the six countries a Claim belongs to: Singapore, Malaysia, Hong Kong, Thailand, Indonesia or the Philippines. Each Market has exactly one currency.
_Avoid_: Region, country, territory

**Info Request**:
A question an Officer asks the Claimant about a Claim, and the Claimant's answer.
_Avoid_: Query, RFI

## Claim status

**Submitted**: reported by the Claimant, not yet picked up by an Officer.

**Under Review**: picked up by an Officer and being assessed.

**Info Requested**: waiting on the Claimant to answer an Info Request.

**Approved**: the Officer has accepted the Claim at its Reserve; not yet paid.

**Settled**: the Reserve has been paid. Final.

**Rejected**: the Officer has declined the Claim. Final.

_Avoid_: Closed, cancelled, withdrawn, pending

## Money

**Reserve**:
The Officer's estimate of what a Claim will cost, in its Market's currency. It can be revised during review and is paid in full on settlement.
_Avoid_: Estimate, payout, settlement amount

**Open Reserves**:
The total Reserve recorded on Claims that are neither Settled nor Rejected, per Market. A recorded figure, not a full liability estimate.
_Avoid_: Liability, outstanding amount

**Unreserved Claim**:
An open Claim with no Reserve yet: exposure nobody has priced.

**Exposure**:
The per-Market picture of outstanding claims: the open Claim count, the Open Reserves and the number of Unreserved Claims.
_Avoid_: Liability

**Workload**:
The Claims each Officer currently holds that are neither Settled nor Rejected.
_Avoid_: Backlog

**Queue**:
The Claims an Officer can act on now: unassigned Submitted Claims plus their own open Claims, oldest first.
_Avoid_: Inbox, worklist

**Claim History**:
The ordered record of every status change and action on a Claim, as the Claimant sees it in their timeline.
_Avoid_: Audit log, activity feed
