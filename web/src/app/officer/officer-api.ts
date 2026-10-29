import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { apiBaseUrl } from '../api';

// Shapes typed by hand against the API's contracts (src/Claims.Api/Claims/ClaimContracts.cs, D27).
// Enums travel as their names; dates as ISO strings.

export type Market = 'SG' | 'MY' | 'HK' | 'TH' | 'ID' | 'PH';
export type ClaimType = 'Motor' | 'Property';
export type ClaimStatus =
  'Submitted' | 'UnderReview' | 'InfoRequested' | 'Approved' | 'Settled' | 'Rejected';

/** One row of the Officer's Queue. */
export interface QueueItem {
  id: string;
  claimNumber: string;
  market: Market;
  claimType: ClaimType;
  status: ClaimStatus;
  createdAt: string;
  isMine: boolean;
}

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

/** The Officer endpoints. Each action returns the updated Claim. */
@Injectable({ providedIn: 'root' })
export class OfficerApi {
  private readonly http = inject(HttpClient);
  private readonly claims = `${apiBaseUrl}/api/claims`;

  queue(): Observable<QueueItem[]> {
    return this.http.get<QueueItem[]>(`${apiBaseUrl}/api/queue`);
  }

  claim(id: string): Observable<Claim> {
    return this.http.get<Claim>(`${this.claims}/${id}`);
  }

  pickUp(id: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.claims}/${id}/pick-up`, null);
  }

  setReserve(id: string, amount: number): Observable<Claim> {
    return this.http.put<Claim>(`${this.claims}/${id}/reserve`, { amount });
  }

  requestInfo(id: string, question: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.claims}/${id}/info-requests`, { question });
  }

  approve(id: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.claims}/${id}/approve`, null);
  }

  reject(id: string, reason: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.claims}/${id}/reject`, { reason });
  }

  settle(id: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.claims}/${id}/settle`, null);
  }
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
 * A failed call in plain words. A broken Claim rule (400, 403, 409) carries the Claim's own message
 * in the problem details' `detail`; a missing or too-long field gets a validation 400 with `errors`.
 */
export function problemMessage(err: HttpErrorResponse): string {
  if (err.status === 0) {
    return 'The API did not answer. Is it running?';
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
