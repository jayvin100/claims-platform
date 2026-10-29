import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { apiBaseUrl } from '../api';

// The Claimant's side of the REST contract, written by hand (D27) to match
// src/Claims.Api/Claims/ClaimContracts.cs. Enums travel as their names; dates as ISO strings.

export type Market = 'SG' | 'MY' | 'HK' | 'TH' | 'ID' | 'PH';
export type ClaimType = 'Motor' | 'Property';
export type ClaimStatus =
  'Submitted' | 'UnderReview' | 'InfoRequested' | 'Approved' | 'Settled' | 'Rejected';

export interface SubmitClaimRequest {
  policyNumber: string;
  market: Market;
  claimType: ClaimType;
  /** yyyy-MM-dd, as a date input gives it. */
  incidentDate: string;
  description: string;
}

/** One row of My Claims. */
export interface ClaimSummary {
  id: string;
  claimNumber: string;
  market: Market;
  claimType: ClaimType;
  status: ClaimStatus;
  createdAt: string;
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
  incidentDate: string;
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

@Injectable({ providedIn: 'root' })
export class ClaimantApi {
  private readonly http = inject(HttpClient);
  private readonly claimsUrl = `${apiBaseUrl}/api/claims`;

  /** The caller's own Claims, newest first (the API sorts them). */
  listMine(): Observable<ClaimSummary[]> {
    return this.http.get<ClaimSummary[]>(this.claimsUrl);
  }

  submit(request: SubmitClaimRequest): Observable<Claim> {
    return this.http.post<Claim>(this.claimsUrl, request);
  }

  get(id: string): Observable<Claim> {
    return this.http.get<Claim>(`${this.claimsUrl}/${id}`);
  }

  /** Answers the open Info Request; returns the Claim, back Under Review. */
  provideInfo(id: string, answer: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.claimsUrl}/${id}/info-response`, { answer });
  }
}
