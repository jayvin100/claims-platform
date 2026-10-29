import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { marketName, poll, problemMessage, statusLabels } from '../claims';
import { ClaimSummary, ClaimantApi } from './claimant-api';

/** The Claimant's landing screen: all their Claims, newest first, refreshed every 5 seconds (D30). */
@Component({
  selector: 'app-my-claims',
  imports: [RouterLink],
  template: `
    <h1>My Claims</h1>
    <p><a routerLink="/my-claims/new">Report a Claim</a></p>

    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    @if (claims(); as claims) {
      @if (claims.length === 0) {
        <p>You have no Claims yet.</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th>Claim Number</th>
              <th>Claim Type</th>
              <th>Market</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            @for (claim of claims; track claim.id) {
              <tr>
                <td>
                  <a [routerLink]="['/my-claims', claim.id]">{{ claim.claimNumber }}</a>
                </td>
                <td>{{ claim.claimType }}</td>
                <td>{{ marketName(claim.market) }}</td>
                <td>{{ statusLabels[claim.status] }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    } @else if (!error()) {
      <p>Loading your Claims…</p>
    }
  `,
})
export class MyClaims {
  private readonly api = inject(ClaimantApi);

  /** null until the first answer arrives. */
  protected readonly claims = signal<ClaimSummary[] | null>(null);
  protected readonly error = signal('');
  protected readonly marketName = marketName;
  protected readonly statusLabels = statusLabels;

  constructor() {
    // Ask now, then every 5 seconds. A failed request shows its error but doesn't stop the polling,
    // and takeUntilDestroyed stops it when the Claimant leaves the screen.
    poll(
      () => this.api.listMine(),
      (err) => this.error.set(problemMessage(err)),
    )
      .pipe(takeUntilDestroyed())
      .subscribe((claims) => {
        this.claims.set(claims);
        this.error.set('');
      });
  }
}
