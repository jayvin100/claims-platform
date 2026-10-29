import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { apiBaseUrl } from '../api';
import { Claim, ClaimStatus, ClaimType, Market } from '../claims';

// The Claimant's own shapes and endpoints, written by hand (D27) to match
// src/Claims.Api/Claims/ClaimContracts.cs. The Claim itself and its helpers are in ../claims.ts.

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
