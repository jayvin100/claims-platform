import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  EMPTY,
  Observable,
  Subject,
  TimeoutError,
  catchError,
  merge,
  switchMap,
  takeUntil,
  timeout,
  timer,
} from 'rxjs';
import {
  Claim,
  dateFormat,
  dateTimeFormat,
  formatAmount,
  marketName,
  problemMessage,
  statusLabels,
} from '../claims';
import { CurrentUser } from '../identity/current-user';
import { OfficerApi } from './officer-api';

/**
 * One Claim as an Officer sees it: details, Reserve, Info Requests and Claim History, with only the
 * actions its status and Assigned Officer allow. The API still checks every rule; hiding buttons
 * just saves the Officer from a refusal they could have predicted.
 */
@Component({
  selector: 'app-claim-detail',
  imports: [RouterLink, DatePipe],
  template: `
    <p><a routerLink="/queue">← Back to the Queue</a></p>

    @if (loadError()) {
      <p class="error">{{ loadError() }}</p>
    }

    @if (claim(); as claim) {
      <h1>{{ claim.claimNumber }}</h1>

      <dl>
        <dt>Status</dt>
        <dd>{{ statusLabels[claim.status] }}</dd>
        <dt>Assigned Officer</dt>
        <dd>
          {{ !claim.assignedOfficerId ? 'Nobody yet' : isMine() ? 'You' : 'Another Officer' }}
        </dd>
        <dt>Reserve</dt>
        <dd>
          @if (claim.reserveAmount === null) {
            Not set
          } @else {
            {{ formatAmount(claim.currency, claim.reserveAmount) }}
          }
        </dd>
        <dt>Market</dt>
        <dd>{{ marketName(claim.market) }} ({{ claim.currency }})</dd>
        <dt>Claim Type</dt>
        <dd>{{ claim.claimType }}</dd>
        <dt>Policy Number</dt>
        <dd>{{ claim.policyNumber }}</dd>
        <dt>Incident date</dt>
        <dd>{{ claim.incidentDate | date: dateFormat }}</dd>
        <dt>Submitted</dt>
        <dd>{{ claim.createdAt | date: dateTimeFormat }}</dd>
        <dt>Description</dt>
        <dd class="text">{{ claim.description }}</dd>
      </dl>

      <section class="actions">
        <h2>Actions</h2>
        @if (actionError()) {
          <p class="error">{{ actionError() }}</p>
        }

        @if (claim.status === 'Submitted') {
          <button type="button" [disabled]="busy()" (click)="pickUp()">Pick up</button>
        } @else if (claim.status === 'Settled' || claim.status === 'Rejected') {
          <!-- Checked before "held by another Officer": a final Claim is final for everyone. -->
          <p>This Claim is {{ statusLabels[claim.status] }}, which is final.</p>
        } @else if (claim.assignedOfficerId && !isMine()) {
          <p>Another Officer holds this Claim, so only they can act on it.</p>
        } @else if (claim.status === 'UnderReview') {
          <form (submit)="$event.preventDefault(); setReserve(amount)">
            <label>
              Reserve ({{ claim.currency }})
              <!-- step="any" so the browser accepts any number of decimals (D61); setReserve checks > 0. -->
              <input #amount type="number" step="any" required />
            </label>
            <button [disabled]="busy()">
              {{ claim.reserveAmount === null ? 'Set Reserve' : 'Revise Reserve' }}
            </button>
          </form>

          <form (submit)="$event.preventDefault(); requestInfo(question)">
            <label>
              Question for the Claimant
              <textarea #question maxlength="1000" required></textarea>
            </label>
            <button [disabled]="busy()">Request info</button>
          </form>

          <p>
            <button
              type="button"
              [disabled]="busy() || claim.reserveAmount === null"
              (click)="approve()"
            >
              Approve
            </button>
            @if (claim.reserveAmount === null) {
              <span class="hint">Set a Reserve before approving.</span>
            }
          </p>

          <form (submit)="$event.preventDefault(); reject(reason)">
            <label>
              Reason for rejection (the Claimant sees it)
              <textarea #reason maxlength="1000" required></textarea>
            </label>
            <button [disabled]="busy()">Reject</button>
          </form>
        } @else if (claim.status === 'InfoRequested') {
          <p>Waiting for the Claimant to answer your Info Request.</p>
        } @else if (claim.status === 'Approved') {
          <button type="button" [disabled]="busy()" (click)="settle()">
            Settle ({{ formatAmount(claim.currency, claim.reserveAmount!) }})
          </button>
        }
      </section>

      <h2>Info Requests</h2>
      @if (claim.infoRequests.length === 0) {
        <p>None yet.</p>
      } @else {
        <ul>
          @for (request of claim.infoRequests; track $index) {
            <li>
              <p>
                <strong>Question</strong> ({{ request.requestedAt | date: dateTimeFormat }}):
                <span class="text">{{ request.question }}</span>
              </p>
              @if (request.answer !== null) {
                <p>
                  <strong>Answer</strong> ({{ request.answeredAt | date: dateTimeFormat }}):
                  <span class="text">{{ request.answer }}</span>
                </p>
              } @else {
                <p><em>Waiting for the Claimant's answer.</em></p>
              }
            </li>
          }
        </ul>
      }

      <h2>Claim History</h2>
      <ol>
        @for (entry of claim.history; track $index) {
          <li>
            {{ entry.occurredAt | date: dateTimeFormat }}:
            <span class="text">{{ entry.description }}</span>
            ({{ statusLabels[entry.status] }})
          </li>
        }
      </ol>
    } @else if (!loadError()) {
      <p>Loading the Claim…</p>
    }
  `,
  styles: `
    dl {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 0.3rem 1rem;
    }
    dt {
      font-weight: 600;
    }
    dd {
      margin: 0;
    }
    .text {
      white-space: pre-wrap;
    }
    .actions form {
      margin-bottom: 1rem;
    }
    label {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      margin-bottom: 0.4rem;
      max-width: 30rem;
    }
    .hint {
      margin-left: 0.5rem;
      color: #666;
    }
  `,
})
export class ClaimDetail {
  private readonly api = inject(OfficerApi);
  private readonly me = inject(CurrentUser).user;
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;

  protected readonly claim = signal<Claim | null>(null);
  protected readonly loadError = signal('');
  protected readonly actionError = signal('');
  /** True while an action is on its way, so a double click can't send it twice. */
  protected readonly busy = signal(false);
  protected readonly isMine = computed(() => this.claim()?.assignedOfficerId === this.me()?.id);
  protected readonly statusLabels = statusLabels;
  protected readonly formatAmount = formatAmount;
  protected readonly dateFormat = dateFormat;
  protected readonly dateTimeFormat = dateTimeFormat;
  protected readonly marketName = marketName;

  /** Fetches at once after an action; switchMap drops any poll still in flight, so it can't overwrite the result. */
  private readonly reload = new Subject<void>();
  /** Fires once if the Claim doesn't exist, to stop polling for it. */
  private readonly notFound = new Subject<void>();

  constructor() {
    // Poll every 5 seconds (D30), as on the Queue; takeUntilDestroyed stops it when the Officer leaves.
    merge(timer(0, 5000), this.reload)
      .pipe(
        switchMap(() =>
          this.api.claim(this.id).pipe(
            timeout(4500), // see problemMessage
            catchError((err: HttpErrorResponse | TimeoutError) => {
              this.loadError.set(problemMessage(err));
              if (err instanceof HttpErrorResponse && err.status === 404) {
                this.notFound.next();
              }
              return EMPTY;
            }),
          ),
        ),
        takeUntil(this.notFound),
        takeUntilDestroyed(),
      )
      .subscribe((claim) => {
        this.claim.set(claim);
        this.loadError.set('');
      });
  }

  protected pickUp(): void {
    // Story 34: a 409 here means another Officer picked the Claim up between our poll and this click.
    this.act(this.api.pickUp(this.id), {
      conflictMessage: 'Another Officer picked up this Claim first.',
    });
  }

  protected setReserve(input: HTMLInputElement): void {
    const amount = Number(input.value);
    if (!(amount > 0)) {
      this.actionError.set('The Reserve must be greater than zero.');
      return;
    }
    this.act(this.api.setReserve(this.id, amount), { done: () => (input.value = '') });
  }

  protected requestInfo(input: HTMLTextAreaElement): void {
    this.act(this.api.requestInfo(this.id, input.value), { done: () => (input.value = '') });
  }

  protected approve(): void {
    this.act(this.api.approve(this.id));
  }

  protected reject(input: HTMLTextAreaElement): void {
    this.act(this.api.reject(this.id, input.value), { done: () => (input.value = '') });
  }

  protected settle(): void {
    this.act(this.api.settle(this.id));
  }

  /**
   * Sends one action. On success shows the updated Claim and runs `done` (to clear a form).
   * On a 409, `conflictMessage` replaces the Claim's own message when the caller knows the cause.
   */
  private act(
    call: Observable<Claim>,
    options: { done?: () => void; conflictMessage?: string } = {},
  ): void {
    this.busy.set(true);
    this.actionError.set('');
    call.subscribe({
      next: (claim) => {
        this.claim.set(claim);
        this.busy.set(false);
        options.done?.();
        this.reload.next();
      },
      // A 403 (another Officer holds it) or 409 (its status changed) reads as the Claim's own message,
      // unless the caller gave a clearer one for the 409.
      error: (err: HttpErrorResponse) => {
        this.actionError.set(
          err.status === 409 && options.conflictMessage
            ? options.conflictMessage
            : problemMessage(err),
        );
        this.busy.set(false);
        this.reload.next(); // show what changed, so the controls match the Claim again
      },
    });
  }
}
