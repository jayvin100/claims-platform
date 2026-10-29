import { HttpErrorResponse } from '@angular/common/http';

// The Claim as the API returns it to both roles (D53), plus helpers both the Officer and the
// Claimant screens use. Shapes typed by hand against the API's contracts
// (src/Claims.Api/Claims/ClaimContracts.cs, D27). Enums travel as their names; dates as ISO strings.

export type Market = 'SG' | 'MY' | 'HK' | 'TH' | 'ID' | 'PH';
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
  reserveAmount: number | null;
  createdAt: string;
  updatedAt: string;
  history: ClaimHistoryEntry[];
  infoRequests: InfoRequest[];
}

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
 * separators and at least two decimals, but never rounded: 20 is the most decimals allowed, more
 * than a JS number holds, so every digit the number has is shown.
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
export function problemMessage(err: HttpErrorResponse): string {
  if (err.status === 0) {
    return 'The API did not answer. Is it running?';
  }
  // The API's 404 says only "Not Found". It means the Claim doesn't exist or, for a Claimant, isn't
  // theirs; the API doesn't say which (D23), so neither do we.
  if (err.status === 404) {
    return 'This Claim was not found.';
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
