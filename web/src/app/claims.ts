import { HttpErrorResponse } from '@angular/common/http';
import {
  EMPTY,
  Observable,
  TimeoutError,
  catchError,
  merge,
  switchMap,
  timeout,
  timer,
} from 'rxjs';

// The Claim as the API returns it to both roles (D53), plus helpers both the Officer and the
// Claimant screens use. Shapes typed by hand against the API's contracts
// (src/Claims.Api/Claims/ClaimContracts.cs, D27). Enums travel as their names; dates as ISO strings.

export type Market = 'SG' | 'MY' | 'HK' | 'TH' | 'ID' | 'PH';

export interface MarketInfo {
  code: Market;
  name: string;
  currency: string;
  timeZone: string;
}

/**
 * The six Markets (D7), with the name and currency people see, and the time zone whose date
 * decides "not in the future" (D47). Mirrors Market.cs.
 */
export const markets: MarketInfo[] = [
  { code: 'SG', name: 'Singapore', currency: 'SGD', timeZone: 'Asia/Singapore' },
  { code: 'MY', name: 'Malaysia', currency: 'MYR', timeZone: 'Asia/Kuala_Lumpur' },
  { code: 'HK', name: 'Hong Kong', currency: 'HKD', timeZone: 'Asia/Hong_Kong' },
  { code: 'TH', name: 'Thailand', currency: 'THB', timeZone: 'Asia/Bangkok' },
  { code: 'ID', name: 'Indonesia', currency: 'IDR', timeZone: 'Asia/Jakarta' },
  { code: 'PH', name: 'Philippines', currency: 'PHP', timeZone: 'Asia/Manila' },
];

/** The Market with this code, or undefined when none is chosen yet (''). */
export function marketFor(code: string): MarketInfo | undefined {
  return markets.find((m) => m.code === code);
}

export function marketName(code: Market): string {
  return marketFor(code)?.name ?? code;
}
export type ClaimType = 'Motor' | 'Property';
export type ClaimStatus =
  'Submitted' | 'UnderReview' | 'InfoRequested' | 'Approved' | 'Settled' | 'Rejected';

export interface ClaimHistoryEntry {
  status: ClaimStatus;
  description: string;
  occurredAt: string;
}

/** answer and answeredAt are null while the Info Request is open. */
export interface InfoRequest {
  question: string;
  answer: string | null;
  requestedAt: string;
  answeredAt: string | null;
}

export interface Claim {
  id: string;
  claimNumber: string;
  policyNumber: string;
  market: Market;
  currency: string;
  claimType: ClaimType;
  incidentDate: string; // a date only, "2026-10-29"
  description: string;
  status: ClaimStatus;
  claimantId: string;
  assignedOfficerId: string | null;
  // A JS number (a double). The API, the database and the Claim History timeline keep the exact decimal,
  // but the UI is only exact to about 15 significant digits, both when a Reserve is typed in and sent and
  // when it is displayed (D73).
  reserveAmount: number | null;
  createdAt: string;
  updatedAt: string;
  history: ClaimHistoryEntry[];
  infoRequests: InfoRequest[];
}

/**
 * DatePipe formats both roles' screens use, so a date reads the same everywhere:
 * "29 Oct 2026" and "29 Oct 2026, 14:08" (in the browser's time zone).
 */
export const dateFormat = 'd MMM y';
export const dateTimeFormat = 'd MMM y, HH:mm';

/** Status names as the glossary writes them. */
export const statusLabels: Record<ClaimStatus, string> = {
  Submitted: 'Submitted',
  UnderReview: 'Under Review',
  InfoRequested: 'Info Requested',
  Approved: 'Approved',
  Settled: 'Settled',
  Rejected: 'Rejected',
};

/**
 * An amount written the way the API's Claim History writes it (D61): "SGD 1,000.005". Thousands
 * separators, at least two decimals and at most 20. Not exact: a JS number holds about 15 significant
 * digits (D73), and anything past 20 decimals is rounded away, so a Reserve of 1e-28 shows as 0.00.
 */
export function formatAmount(currency: string, amount: number): string {
  const digits = amount.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 20,
  });
  return `${currency} ${digits}`;
}

/**
 * A failed call in plain words. A broken Claim rule (400, 403, 409) carries the Claim's own message
 * in the problem details' `detail`; a missing or too-long field gets a validation 400 with `errors`.
 */
export function problemMessage(err: HttpErrorResponse | TimeoutError): string {
  // Only poll() times a request out; see there.
  if (err instanceof TimeoutError) {
    return 'The server took too long to answer.';
  }
  if (err.status === 0) {
    return 'The API did not answer. Is it running?';
  }
  // The API's 404 says only "Not Found". It means the Claim doesn't exist or, for a Claimant, isn't
  // theirs; the API doesn't say which (D23), so neither do we.
  if (err.status === 404) {
    return 'This Claim was not found.';
  }
  // The API doesn't know the demo user this browser sent (for example, the database was reset).
  if (err.status === 401) {
    return "Your demo user isn't recognised. Use Switch user.";
  }
  const body = err.error;
  if (body?.errors) {
    // { "Reason": ["The Reason field is required."], ... } -> one line
    return Object.values(body.errors as Record<string, string[]>)
      .flat()
      .join(' ');
  }
  return body?.detail ?? body?.title ?? `Something went wrong (HTTP ${err.status}).`;
}

/** How often a screen asks the API again (D30). */
const pollEvery = 5000;
/**
 * How long each polled GET may take (D92): shorter than the 5 s tick, so a slow request fails with
 * a message before the next tick's switchMap would cancel it silently. Action calls (pick up,
 * approve, submit) have no timeout.
 */
const pollTimeout = 4500;

/**
 * Calls `request` now, then every 5 seconds, and also whenever `refresh` fires. switchMap drops a
 * request still in flight when the next one starts, so a slow, older answer can't overwrite a newer
 * one (for example the fresh Claim fetched right after an action). A failed request is passed to
 * `onError` and skipped: the polling goes on, and the screen keeps what it last showed.
 * The screen adds takeUntilDestroyed(), so the polling stops when the user leaves it.
 */
export function poll<T>(
  request: () => Observable<T>,
  onError: (err: HttpErrorResponse | TimeoutError) => void,
  refresh: Observable<void> = EMPTY,
): Observable<T> {
  return merge(timer(0, pollEvery), refresh).pipe(
    switchMap(() =>
      request().pipe(
        timeout(pollTimeout),
        catchError((err: HttpErrorResponse | TimeoutError) => {
          onError(err);
          return EMPTY;
        }),
      ),
    ),
  );
}
