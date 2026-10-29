import { HttpErrorResponse } from '@angular/common/http';
import { ClaimStatus, Market } from './claimant-api';

// Small helpers the Claimant screens share for turning API values into plain words.

/** The six Markets (D7), with the name and currency a Claimant sees. Mirrors Market.cs. */
export const markets: { code: Market; name: string; currency: string }[] = [
  { code: 'SG', name: 'Singapore', currency: 'SGD' },
  { code: 'MY', name: 'Malaysia', currency: 'MYR' },
  { code: 'HK', name: 'Hong Kong', currency: 'HKD' },
  { code: 'TH', name: 'Thailand', currency: 'THB' },
  { code: 'ID', name: 'Indonesia', currency: 'IDR' },
  { code: 'PH', name: 'Philippines', currency: 'PHP' },
];

export function marketName(code: Market): string {
  return markets.find((m) => m.code === code)?.name ?? code;
}

/** The glossary's status names: "UnderReview" reads as "Under Review". */
export function statusLabel(status: ClaimStatus): string {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

/**
 * A money amount with thousands separators and at least two decimals, never rounded (D61),
 * so it matches the "Reserve set to …" line on the timeline.
 */
export function formatAmount(currency: string, amount: number): string {
  const shown = amount.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 20,
  });
  return `${currency} ${shown}`;
}

/**
 * An API error as lines to show: the problem-details message first, then each validation error.
 * A status of 0 means the request never got an answer.
 */
export function errorLines(err: HttpErrorResponse): string[] {
  if (err.status === 0) {
    return ['Could not reach the API. Is it running?'];
  }
  // The API's 404 says only "Not Found". For a Claimant it means the Claim doesn't exist or isn't
  // theirs; the API doesn't say which (D23), so neither do we.
  if (err.status === 404) {
    return ['This Claim was not found.'];
  }
  const body = err.error;
  const message: string =
    body?.detail ?? body?.title ?? `Something went wrong (HTTP ${err.status}).`;
  // A validation problem carries { errors: { field: [messages] } }.
  const fieldErrors = Object.values(body?.errors ?? {}).flat() as string[];
  return [message, ...fieldErrors];
}
