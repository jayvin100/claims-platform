import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, Observable, Subject, catchError, merge, switchMap, timer } from 'rxjs';
import { CurrentUser } from '../identity/current-user';
import { Claim, OfficerApi, problemMessage, statusLabels } from './officer-api';

/**
 * One Claim as an Officer sees it: details, Reserve, Info Requests and timeline, with only the
 * actions its status and Assigned Officer allow. The API still checks every rule; hiding buttons
 * just saves the Officer from a refusal they could have predicted.
 */
@Component({
  selector: 'app-claim-detail',
  imports: [RouterLink, DatePipe, DecimalPipe],
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
          <!-- Written as the API's timeline writes it, "SGD 1,500.00": currency code, then amount. -->
          @if (claim.reserveAmount === null) {
            Not set
          } @else {
            {{ claim.currency }} {{ claim.reserveAmount | number: '1.2-10' }}
          }
        </dd>
        <dt>Market</dt>
        <dd>{{ claim.market }} ({{ claim.currency }})</dd>
        <dt>Claim Type</dt>
        <dd>{{ claim.claimType }}</dd>
        <dt>Policy Number</dt>
        <dd>{{ claim.policyNumber }}</dd>
        <dt>Incident date</dt>
        <dd>{{ claim.incidentDate }}</dd>
        <dt>Submitted</dt>
        <dd>{{ claim.createdAt | date: 'medium' }}</dd>
        <dt>Description</dt>
        <dd class="text">{{ claim.description }}</dd>
      </dl>

      <section class="actions">
        <h2>Actions</h2>
        @if (actionError()) {
          <p class="error">{{ actionError() }}</p>
        }

        @if (claim.status === 'Submitted') {
          <button type="button" [disabled]="busy()" (click)="act(api.pickUp(claim.id))">
            Pick up
          </button>
        } @else if (claim.assignedOfficerId && !isMine()) {
          <p>Another Officer holds this Claim, so only they can act on it.</p>
        } @else if (claim.status === 'UnderReview') {
          <form (submit)="$event.preventDefault(); setReserve(claim.id, amount)">
            <label>
              Reserve ({{ claim.currency }})
              <input #amount type="number" min="0.01" step="0.01" required />
            </label>
            <button [disabled]="busy()">
              {{ claim.reserveAmount === null ? 'Set Reserve' : 'Revise Reserve' }}
            </button>
          </form>

          <form (submit)="$event.preventDefault(); requestInfo(claim.id, question)">
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
              (click)="act(api.approve(claim.id))"
            >
              Approve
            </button>
            @if (claim.reserveAmount === null) {
              <span class="hint">Set a Reserve before approving.</span>
            }
          </p>

          <form (submit)="$event.preventDefault(); reject(claim.id, reason)">
            <label>
              Reason for rejection (the Claimant sees it)
              <textarea #reason maxlength="1000" required></textarea>
            </label>
            <button [disabled]="busy()">Reject</button>
          </form>
        } @else if (claim.status === 'InfoRequested') {
          <p>Waiting for the Claimant to answer your Info Request.</p>
        } @else if (claim.status === 'Approved') {
          <button type="button" [disabled]="busy()" (click)="act(api.settle(claim.id))">
            Settle ({{ claim.currency }} {{ claim.reserveAmount | number: '1.2-10' }})
          </button>
        } @else {
          <p>This Claim is {{ statusLabels[claim.status] }}, which is final.</p>
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
                <strong>Asked {{ request.requestedAt | date: 'medium' }}:</strong>
                {{ request.question }}
              </p>
              @if (request.answer !== null) {
                <p>
                  <strong>Answered {{ request.answeredAt | date: 'medium' }}:</strong>
                  {{ request.answer }}
                </p>
              } @else {
                <p><em>Waiting for the Claimant's answer.</em></p>
              }
            </li>
          }
        </ul>
      }

      <h2>Timeline</h2>
      <ol>
        @for (entry of claim.history; track $index) {
          <li>
            {{ entry.occurredAt | date: 'medium' }}: {{ entry.description }} ({{
              statusLabels[entry.status]
            }})
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
  protected readonly api = inject(OfficerApi);
  private readonly me = inject(CurrentUser).user;
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;

  protected readonly claim = signal<Claim | null>(null);
  protected readonly loadError = signal('');
  protected readonly actionError = signal('');
  /** True while an action is on its way, so a double click can't send it twice. */
  protected readonly busy = signal(false);
  protected readonly isMine = computed(() => this.claim()?.assignedOfficerId === this.me()?.id);
  protected readonly statusLabels = statusLabels;

  /** Fetches at once after an action; switchMap drops any poll still in flight, so it can't overwrite the result. */
  private readonly reload = new Subject<void>();

  constructor() {
    // Poll every 5 seconds (D30), as on the Queue; takeUntilDestroyed stops it when the Officer leaves.
    merge(timer(0, 5000), this.reload)
      .pipe(
        switchMap(() =>
          this.api.claim(this.id).pipe(
            catchError((err: HttpErrorResponse) => {
              this.loadError.set(
                err.status === 404 ? 'There is no such Claim.' : problemMessage(err),
              );
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((claim) => {
        this.claim.set(claim);
        this.loadError.set('');
      });
  }

  /** Sends one action. On success shows the updated Claim and runs `done` (to clear a form). */
  protected act(call: Observable<Claim>, done?: () => void): void {
    this.busy.set(true);
    this.actionError.set('');
    call.subscribe({
      next: (claim) => {
        this.claim.set(claim);
        this.busy.set(false);
        done?.();
        this.reload.next();
      },
      // A 403 (another Officer holds it) or 409 (its status changed) reads as the Claim's own message.
      error: (err: HttpErrorResponse) => {
        this.actionError.set(problemMessage(err));
        this.busy.set(false);
        this.reload.next(); // show what changed, so the controls match the Claim again
      },
    });
  }

  protected setReserve(id: string, input: HTMLInputElement): void {
    this.act(this.api.setReserve(id, Number(input.value)), () => (input.value = ''));
  }

  protected requestInfo(id: string, input: HTMLTextAreaElement): void {
    this.act(this.api.requestInfo(id, input.value), () => (input.value = ''));
  }

  protected reject(id: string, input: HTMLTextAreaElement): void {
    this.act(this.api.reject(id, input.value), () => (input.value = ''));
  }
}
