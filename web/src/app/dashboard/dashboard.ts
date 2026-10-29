import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, catchError, forkJoin, switchMap, timer } from 'rxjs';
import { formatAmount, problemMessage } from '../claims';
import { DemoUser } from '../identity/demo-user';
import { UsersApi } from '../identity/users-api';
import { MarketExposure, OfficerWorkload, ReportsApi, Workload } from './reports-api';

/**
 * The Team's Workload and the Exposure per Market (D17). Both come from the Kafka-fed read model,
 * so they trail the Claims by a few seconds (ADR-0003).
 */
@Component({
  selector: 'app-dashboard',
  template: `
    <h1>Dashboard</h1>
    <p class="note">These figures are fed by events, so they can lag the live Claims by a few seconds.</p>

    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    @if (workload(); as workload) {
      <h2>Workload</h2>
      <p>Submitted Claims nobody has picked up: <strong>{{ workload.unassignedSubmitted }}</strong></p>
      @if (officerRows().length === 0) {
        <p>No Officers to show yet.</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th>Officer</th>
              <th>Under Review</th>
              <th>Info Requested</th>
              <th>Approved</th>
              <th>Settled</th>
              <th>Rejected</th>
            </tr>
          </thead>
          <tbody>
            @for (row of officerRows(); track row.officerId) {
              <tr>
                <td>{{ row.name }}</td>
                <td>{{ row.underReview }}</td>
                <td>{{ row.infoRequested }}</td>
                <td>{{ row.approved }}</td>
                <td>{{ row.settled }}</td>
                <td>{{ row.rejected }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    }

    @if (exposure(); as exposure) {
      <h2>Exposure</h2>
      <!-- One row per Market, each in its own currency: there is no grand total, since that would add currencies together. -->
      <table>
        <thead>
          <tr>
            <th>Market</th>
            <th>Open Claims</th>
            <th>Open Reserves</th>
            <th>Unreserved</th>
          </tr>
        </thead>
        <tbody>
          @for (row of exposure; track row.market) {
            <tr>
              <td>{{ row.market }}</td>
              <td>{{ row.openClaims }}</td>
              <td>{{ formatAmount(row.currency, row.openReserves) }}</td>
              <td>{{ row.unreservedClaims }}</td>
            </tr>
          }
        </tbody>
      </table>
      <p class="note">Unreserved: open Claims with no Reserve yet, so they are not in Open Reserves.</p>
    }

    @if (!workload() && !exposure() && !error()) {
      <p>Loading the Dashboard…</p>
    }
  `,
  styles: `
    table {
      border-collapse: collapse;
    }
    th,
    td {
      text-align: left;
      padding: 0.4rem 1rem 0.4rem 0;
      border-bottom: 1px solid #ddd;
    }
    .note {
      color: #555;
    }
  `,
})
export class Dashboard {
  private readonly reports = inject(ReportsApi);
  private readonly users = inject(UsersApi);

  /** null until the first answer. */
  protected readonly workload = signal<Workload | null>(null);
  protected readonly exposure = signal<MarketExposure[] | null>(null);
  protected readonly error = signal('');
  /** The seeded Officers, from GET /api/users; Reporting itself only knows ids. */
  private readonly officers = signal<DemoUser[]>([]);
  protected readonly formatAmount = formatAmount;

  /**
   * Every Officer, sorted by name, with zeros for anyone Reporting hasn't seen pick up a Claim
   * (story 51: each Officer). An id Reporting knows but the users list doesn't is shown as the id.
   */
  protected readonly officerRows = computed(() => {
    const counts = new Map((this.workload()?.officers ?? []).map((o) => [o.officerId, o]));
    const known = this.officers()
      .map((user) => ({ ...(counts.get(user.id) ?? noClaims(user.id)), name: user.name }))
      .sort((a, b) => a.name.localeCompare(b.name));
    // Unknown ids go last, so they don't sort in among the names.
    const unknown = [...counts.values()]
      .filter((o) => !this.officers().some((user) => user.id === o.officerId))
      .map((o) => ({ ...o, name: o.officerId }));
    return [...known, ...unknown];
  });

  constructor() {
    // The seeded users don't change, so they are loaded once. If this fails, only Reporting's ids show.
    this.users
      .list()
      .pipe(
        catchError(() => EMPTY),
        takeUntilDestroyed(),
      )
      .subscribe((users) => this.officers.set(users.filter((u) => u.role === 'Officer')));

    // Poll every 5 seconds (D30), both reports together. switchMap drops a slow request when the next
    // one starts, and takeUntilDestroyed stops the timer when the Officer leaves the screen.
    timer(0, 5000)
      .pipe(
        switchMap(() =>
          forkJoin([this.reports.workload(), this.reports.exposure()]).pipe(
            // A failed poll shows a message and leaves the last figures on screen; the next tick tries again.
            catchError((err: HttpErrorResponse) => {
              this.error.set(problemMessage(err));
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(([workload, exposure]) => {
        this.workload.set(workload);
        this.exposure.set(exposure);
        this.error.set('');
      });
  }
}

/** The row for an Officer who has no Claims in Reporting yet. */
function noClaims(officerId: string): OfficerWorkload {
  return { officerId, underReview: 0, infoRequested: 0, approved: 0, settled: 0, rejected: 0 };
}
