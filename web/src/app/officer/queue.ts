import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { EMPTY, catchError, switchMap, timer } from 'rxjs';
import { marketName, problemMessage, statusLabels } from '../claims';
import { OfficerApi, QueueItem } from './officer-api';

/**
 * The Officer's Queue: unassigned Submitted Claims plus their own open Claims. The API already
 * filters and sorts it oldest first (D18, D57), so the screen shows the rows in the order they come.
 */
@Component({
  selector: 'app-queue',
  imports: [RouterLink],
  template: `
    <h1>Queue</h1>

    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    @if (items(); as items) {
      @if (items.length === 0) {
        <p>Your Queue is empty: no Submitted Claims are waiting and you hold no open Claims.</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th>Claim Number</th>
              <th>Market</th>
              <th>Claim Type</th>
              <th>Status</th>
              <th>Age</th>
              <th>Mine</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr [class.mine]="item.isMine">
                <td>
                  <a [routerLink]="['/queue', item.id]">{{ item.claimNumber }}</a>
                </td>
                <td>{{ marketName(item.market) }}</td>
                <td>{{ item.claimType }}</td>
                <td>{{ statusLabels[item.status] }}</td>
                <td>{{ age(item.createdAt) }}</td>
                <td>{{ item.isMine ? 'Mine' : '' }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    } @else if (!error()) {
      <p>Loading the Queue…</p>
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
    tr.mine td {
      font-weight: 600;
    }
  `,
})
export class Queue {
  private readonly api = inject(OfficerApi);

  /** null until the first answer. */
  protected readonly items = signal<QueueItem[] | null>(null);
  protected readonly error = signal('');
  /** When the rows were last loaded; ages are worked out from it, so they move on with each poll. */
  private readonly now = signal(Date.now());
  protected readonly statusLabels = statusLabels;
  protected readonly marketName = marketName;

  constructor() {
    // Poll every 5 seconds (D30). switchMap drops a slow request when the next one starts, and
    // takeUntilDestroyed stops the timer when the Officer leaves the screen.
    timer(0, 5000)
      .pipe(
        switchMap(() =>
          this.api.queue().pipe(
            // A failed poll shows a message and leaves the last rows on screen; the next tick tries again.
            catchError((err: HttpErrorResponse) => {
              this.error.set(problemMessage(err));
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((items) => {
        this.items.set(items);
        this.error.set('');
        this.now.set(Date.now());
      });
  }

  /** "12 min", "5 h" or "3 d" since the Claim was submitted. */
  protected age(createdAt: string): string {
    const minutes = Math.max(0, Math.floor((this.now() - Date.parse(createdAt)) / 60_000));
    if (minutes < 60) return `${minutes} min`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours} h`;
    return `${Math.floor(hours / 24)} d`;
  }
}
