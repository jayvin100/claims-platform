import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { apiBaseUrl } from '../api';
import { Market } from '../claims';

// The report shapes, typed by hand against src/Claims.Api/Reporting/ReportingContracts.cs (D27).

/** One Market's Exposure. openReserves is in this row's own currency. */
export interface MarketExposure {
  market: Market;
  currency: string;
  openClaims: number;
  openReserves: number;
  unreservedClaims: number;
}

/** One Officer's Claims by status. Reporting sends the id only; the name comes from GET /api/users. */
export interface OfficerWorkload {
  officerId: string;
  underReview: number;
  infoRequested: number;
  approved: number;
  settled: number;
  rejected: number;
}

export interface Workload {
  unassignedSubmitted: number;
  officers: OfficerWorkload[];
}

/** The Officer-only report endpoints, read from the Kafka-fed read model. */
@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);
  private readonly reportsUrl = `${apiBaseUrl}/api/reports`;

  exposure(): Observable<MarketExposure[]> {
    return this.http.get<MarketExposure[]>(`${this.reportsUrl}/exposure`);
  }

  workload(): Observable<Workload> {
    return this.http.get<Workload>(`${this.reportsUrl}/workload`);
  }
}
