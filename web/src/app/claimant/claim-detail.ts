import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, merge, switchMap, takeUntil, timer } from 'rxjs';
import {
  Claim,
  dateFormat,
  dateTimeFormat,
  formatAmount,
  marketName,
  problemMessage,
  statusLabels,
} from '../claims';
import { ClaimantApi } from './claimant-api';

// The Claim's rejection reason is kept in its Claim History entry, not a field of its own (D62).
const rejectedPrefix = 'Claim rejected: ';

/**
 * One of the Claimant's Claims: its details, the Claim History timeline, the Info Requests and,
 * while one is open, a box to answer it. Refreshed every 5 seconds (D30).
 */
@Component({
  selector: 'app-claim-detail',
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  template: `
    <p><a routerLink="/my-claims">Back to My Claims</a></p>

    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    @if (claim(); as claim) {
      <h1>{{ claim.claimNumber }}</h1>
      <p class="status">{{ statusLabels[claim.status] }}</p>

      @if (rejectionReason(); as reason) {
        <p class="decision">Your Claim was rejected. Reason: {{ reason }}</p>
      }

      <dl>
        <dt>Policy Number</dt>
        <dd>{{ claim.policyNumber }}</dd>
        <dt>Market</dt>
        <dd>{{ marketName(claim.market) }} ({{ claim.currency }})</dd>
        <dt>Claim Type</dt>
        <dd>{{ claim.claimType }}</dd>
        <dt>Incident date</dt>
        <dd>{{ claim.incidentDate | date: dateFormat }}</dd>
        <dt>Description</dt>
        <dd class="text">{{ claim.description }}</dd>
        @if (claim.reserveAmount !== null) {
          <dt>Reserve</dt>
          <dd>{{ formatAmount(claim.currency, claim.reserveAmount) }}</dd>
        }
      </dl>

      <h2>Info Requests</h2>
      @for (request of claim.infoRequests; track $index) {
        <div class="info-request">
          <p>
            <strong>Question</strong> ({{ request.requestedAt | date: dateTimeFormat }}):
            <span class="text">{{ request.question }}</span>
          </p>
          @if (request.answer !== null) {
            <p>
              <strong>Your answer</strong> ({{ request.answeredAt | date: dateTimeFormat }}):
              <span class="text">{{ request.answer }}</span>
            </p>
          } @else {
            <!-- The answer box shows only while this Info Request is open. -->
            <!-- [formGroup] is what makes (ngSubmit) fire and stops the browser's own page-reloading submit. -->
            <form [formGroup]="answerForm" (ngSubmit)="sendAnswer()">
              <label>
                Your answer
                <textarea formControlName="answer" rows="4" maxlength="2000"></textarea>
              </label>
              @if (answerError()) {
                <p class="error">{{ answerError() }}</p>
              }
              <button type="submit" [disabled]="sending()">
                {{ sending() ? 'Sending…' : 'Send answer' }}
              </button>
            </form>
          }
        </div>
      } @empty {
        <p>No Info Requests.</p>
      }

      <h2>Claim History</h2>
      <ol class="timeline">
        @for (entry of claim.history; track $index) {
          <li>
            <span class="when">{{ entry.occurredAt | date: dateTimeFormat }}</span>
            <span class="text">{{ entry.description }}</span>
            <span class="status-tag">{{ statusLabels[entry.status] }}</span>
          </li>
        }
      </ol>
    } @else if (!error()) {
      <p>Loading the Claim…</p>
    }
  `,
  styles: `
    .status {
      font-weight: bold;
    }
    .decision {
      border-left: 4px solid #b00020;
      padding-left: 0.75rem;
    }
    dl {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 0.25rem 1rem;
    }
    dd {
      margin: 0;
    }
    /* Keep the Claimant's and Officer's line breaks. */
    .text {
      white-space: pre-wrap;
    }
    .info-request {
      border: 1px solid #ddd;
      padding: 0 0.75rem;
      margin-bottom: 0.75rem;
      max-width: 40rem;
    }
    form,
    label {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    form {
      margin-bottom: 0.75rem;
    }
    form button {
      align-self: flex-start;
    }
    .timeline li {
      margin-bottom: 0.25rem;
    }
    .when {
      color: #555;
      margin-right: 0.75rem;
    }
    .status-tag {
      margin-left: 0.75rem;
      color: #555;
    }
  `,
})
export class ClaimDetail {
  private readonly api = inject(ClaimantApi);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  /** null until the first answer arrives. */
  protected readonly claim = signal<Claim | null>(null);
  /** Why the Claim couldn't be loaded. */
  protected readonly error = signal('');

  protected readonly answerForm = new FormGroup({
    answer: new FormControl('', { nonNullable: true }),
  });
  protected readonly sending = signal(false);
  protected readonly answerError = signal('');

  /** Shown only when Rejected: the reason the Officer gave, from the last Claim History entry. */
  protected readonly rejectionReason = computed(() => {
    const claim = this.claim();
    if (claim?.status !== 'Rejected') {
      return '';
    }
    const entry = claim.history.filter((h) => h.status === 'Rejected').at(-1);
    return entry?.description.startsWith(rejectedPrefix)
      ? entry.description.slice(rejectedPrefix.length)
      : (entry?.description ?? '');
  });

  protected readonly marketName = marketName;
  protected readonly statusLabels = statusLabels;
  protected readonly formatAmount = formatAmount;
  protected readonly dateFormat = dateFormat;
  protected readonly dateTimeFormat = dateTimeFormat;

  /** Fetches the Claim at once, outside the 5-second rhythm. */
  private readonly refresh = new Subject<void>();
  /** Fires once if the Claim doesn't exist (or isn't theirs), to stop polling for it. */
  private readonly notFound = new Subject<void>();

  constructor() {
    // Ask now, then every 5 seconds, and also whenever refresh fires. switchMap drops a request still
    // in flight when the next one starts, so a slow, older answer can't overwrite a newer one.
    merge(timer(0, 5000), this.refresh)
      .pipe(
        switchMap(() =>
          this.api.get(this.id).pipe(
            catchError((err: HttpErrorResponse) => {
              this.error.set(problemMessage(err));
              if (err.status === 404) {
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
        this.error.set('');
        // Once the Info Request is answered (here or in another tab), its old error no longer applies.
        if (claim.status !== 'InfoRequested') {
          this.answerError.set('');
        }
      });
  }

  protected sendAnswer(): void {
    this.answerError.set('');
    const answer = this.answerForm.controls.answer.value;
    if (answer.trim() === '') {
      this.answerError.set('Write your answer before sending it.');
      return;
    }
    this.sending.set(true);
    this.api.provideInfo(this.id, answer).subscribe({
      next: (claim) => {
        this.claim.set(claim);
        this.answerForm.reset();
        this.sending.set(false);
        // A poll sent before the answer was saved could still come back with the old, open request;
        // refreshing now cancels it.
        this.refresh.next();
      },
      error: (err: HttpErrorResponse) => {
        this.answerError.set(problemMessage(err));
        this.sending.set(false);
      },
    });
  }
}
