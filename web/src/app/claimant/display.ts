import { Market } from '../claims';

// The Market details only the Claimant screens need. The Claim types and the helpers both roles
// share (status names, amounts, error messages) are in ../claims.ts.

/**
 * The six Markets (D7), with the name and currency a Claimant sees, and the time zone whose date
 * decides "not in the future" (D47). Mirrors Market.cs.
 */
export const markets: { code: Market; name: string; currency: string; timeZone: string }[] = [
  { code: 'SG', name: 'Singapore', currency: 'SGD', timeZone: 'Asia/Singapore' },
  { code: 'MY', name: 'Malaysia', currency: 'MYR', timeZone: 'Asia/Kuala_Lumpur' },
  { code: 'HK', name: 'Hong Kong', currency: 'HKD', timeZone: 'Asia/Hong_Kong' },
  { code: 'TH', name: 'Thailand', currency: 'THB', timeZone: 'Asia/Bangkok' },
  { code: 'ID', name: 'Indonesia', currency: 'IDR', timeZone: 'Asia/Jakarta' },
  { code: 'PH', name: 'Philippines', currency: 'PHP', timeZone: 'Asia/Manila' },
];

export function marketName(code: Market): string {
  return markets.find((m) => m.code === code)?.name ?? code;
}

/** Today's date in the time zone, as YYYY-MM-DD: the 'en-CA' locale writes dates that way. */
export function todayIn(timeZone: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone }).format(new Date());
}
