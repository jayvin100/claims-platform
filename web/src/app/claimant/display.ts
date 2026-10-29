// A date helper only the Claimant screens need. The Markets, the Claim types and the helpers both
// roles share (status names, amounts, error messages) are in ../claims.ts.

/** Today's date in the time zone, as YYYY-MM-DD: the 'en-CA' locale writes dates that way. */
export function todayIn(timeZone: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone }).format(new Date());
}
