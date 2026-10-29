import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { apiBaseUrl } from '../api';
import { Claim, ClaimStatus, ClaimType, Market } from '../claims';

// The Officer-only shapes and endpoints; the Claim itself and its helpers are in ../claims.ts.

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

/** The Officer endpoints. Each action returns the updated Claim. */
@Injectable({ providedIn: 'root' })
export class OfficerApi {
  private readonly http = inject(HttpClient);
  private readonly claimsUrl = `${apiBaseUrl}/api/claims`;

  queue(): Observable<QueueItem[]> {
    return this.http.get<QueueItem[]>(`${apiBaseUrl}/api/queue`);
  }

  claim(id: string): Observable<Claim> {
    return this.http.get<Claim>(`${this.claimsUrl}/${id}`);
  }

  pickUp(id: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.claimsUrl}/${id}/pick-up`, null);
  }

  setReserve(id: string, amount: number): Observable<Claim> {
    return this.http.put<Claim>(`${this.claimsUrl}/${id}/reserve`, { amount });
  }

  requestInfo(id: string, question: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.claimsUrl}/${id}/info-requests`, { question });
  }

  approve(id: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.claimsUrl}/${id}/approve`, null);
  }

  reject(id: string, reason: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.claimsUrl}/${id}/reject`, { reason });
  }

  settle(id: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.claimsUrl}/${id}/settle`, null);
  }
}
