import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  EMPTY,
  Observable,
  TimeoutError,
  catchError,
  forkJoin,
  map,
  of,
  switchMap,
  timeout,
  timer,
} from 'rxjs';
import { formatAmount, marketName, problemMessage, statusLabels } from '../claims';
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
    <p class="note">
      These figures are fed by events, so they can lag the live Claims by a few seconds.
    </p>

    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    @if (workload(); as workload) {
      <h2>Team Workload</h2>
      <p>
        Submitted Claims nobody has picked up: <strong>{{ workload.unassignedSubmitted }}</strong>
      </p>
      @if (officerRows().length === 0) {
        <p>No Officers to show yet.</p>
      } @else {
        <table>
          <!-- Workload is the open Claims; Settled and Rejected sit beside it as the Officer's output (D77). -->
          <thead>
            <tr>
              <th></th>
              <th colspan="3">Open (Workload)</th>
              <th colspan="2">Output</th>
            </tr>
            <tr>
              <th>Officer</th>
              <th>{{ statusLabels.UnderReview }}</th>
              <th>{{ statusLabels.InfoRequested }}</th>
              <th>{{ statusLabels.Approved }}</th>
              <th>{{ statusLabels.Settled }}</th>
              <th>{{ statusLabels.Rejected }}</th>
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
              <td>{{ marketName(row.market) }}</td>
              <td>{{ row.openClaims }}</td>
              <td>{{ formatAmount(row.currency, row.openReserves) }}</td>
              <td>{{ row.unreservedClaims }}</td>
            </tr>
          }
        </tbody>
      </table>
      <p class="note">
        Unreserved: open Claims with no Reserve yet, so they are not in Open Reserves.
      </p>
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
  /** The seeded Officers, from GET /api/users; Reporting itself only knows ids. null until loaded. */
  private readonly officers = signal<DemoUser[] | null>(null);
  protected readonly formatAmount = formatAmount;
  protected readonly statusLabels = statusLabels;
  protected readonly marketName = marketName;

  /**
   * Every Officer, sorted by name, with zeros for anyone Reporting hasn't seen pick up a Claim
   * (story 51: each Officer). An id Reporting knows but the users list doesn't is shown as the id.
   */
  protected readonly officerRows = computed(() => {
    const officers = this.officers() ?? [];
    const workloadById = new Map((this.workload()?.officers ?? []).map((o) => [o.officerId, o]));
    const known = officers
      .map((user) => ({
        ...(workloadById.get(user.id) ?? emptyWorkload(user.id)),
        name: user.name,
      }))
      .sort((a, b) => a.name.localeCompare(b.name));
    // Unknown ids go last, so they don't sort in among the names.
    const knownIds = new Set(officers.map((user) => user.id));
    const unknown = [...workloadById.values()]
      .filter((o) => !knownIds.has(o.officerId))
      .map((o) => ({ ...o, name: o.officerId }));
    return [...known, ...unknown];
  });

  constructor() {
    // Poll every 5 seconds (D30), both reports together. switchMap drops a slow request when the next
    // one starts, and takeUntilDestroyed stops the timer when the Officer leaves the screen.
    // The seeded users don't change, so they are fetched in the same poll only until they load once;
    // after that the poll reuses them. Until then nothing shows, so no Officer is ever missing (D75).
    timer(0, 5000)
      .pipe(
        switchMap(() =>
          forkJoin([this.reports.workload(), this.reports.exposure(), this.loadOfficers()]).pipe(
            timeout(4500), // all three GETs together; see problemMessage
            // A failed poll shows a message and leaves the last figures on screen; the next tick tries again.
            catchError((err: HttpErrorResponse | TimeoutError) => {
              this.error.set(problemMessage(err));
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(([workload, exposure, officers]) => {
        this.officers.set(officers);
        this.workload.set(workload);
        this.exposure.set(exposure);
        this.error.set('');
      });
  }

  /** The Officers already loaded, or a fetch of them from GET /api/users. */
  private loadOfficers(): Observable<DemoUser[]> {
    const officers = this.officers();
    if (officers) {
      return of(officers);
    }
    return this.users.list().pipe(map((users) => users.filter((u) => u.role === 'Officer')));
  }
}

/** The row for an Officer who has no Claims in Reporting yet. */
function emptyWorkload(officerId: string): OfficerWorkload {
  return { officerId, underReview: 0, infoRequested: 0, approved: 0, settled: 0, rejected: 0 };
}
