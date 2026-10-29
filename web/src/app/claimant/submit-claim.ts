import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Claim, ClaimType, ClaimantApi, Market } from './claimant-api';
import { errorLines, markets, statusLabel } from './display';

// Spaces alone pass Validators.required but the API refuses them, so catch them here too.
const notBlank = Validators.pattern(/\S/);

/**
 * The Claimant reports a Claim. The form checks what it can before sending (required fields and
 * lengths, the same limits as SubmitClaimRequest); the API checks everything again, including that
 * the incident date isn't in the future on the Market's own clock, and its message is shown as is.
 */
@Component({
  selector: 'app-submit-claim',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <h1>Report a Claim</h1>
    <p><a routerLink="/my-claims">Back to My Claims</a></p>

    @if (submitted(); as claim) {
      <p class="confirmation">
        Your Claim Number is <strong>{{ claim.claimNumber }}</strong
        >. Its status is {{ statusLabel(claim.status) }}.
      </p>
      <p><a [routerLink]="['/my-claims', claim.id]">Open the Claim</a></p>
    } @else {
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>
          Policy Number
          <input formControlName="policyNumber" maxlength="50" />
        </label>
        @if (showError('policyNumber')) {
          <p class="error">Enter your Policy Number.</p>
        }

        <label>
          Market
          <select formControlName="market">
            <option value="" disabled>Choose a Market</option>
            @for (market of markets; track market.code) {
              <option [value]="market.code">{{ market.name }}</option>
            }
          </select>
        </label>
        @if (currency(); as currency) {
          <p class="hint">Amounts on this Claim will be in {{ currency }}.</p>
        }
        @if (showError('market')) {
          <p class="error">Choose a Market.</p>
        }

        <label>
          Claim Type
          <select formControlName="claimType">
            <option value="" disabled>Choose a Claim Type</option>
            <option value="Motor">Motor</option>
            <option value="Property">Property</option>
          </select>
        </label>
        @if (showError('claimType')) {
          <p class="error">Choose a Claim Type.</p>
        }

        <label>
          Incident date
          <input type="date" formControlName="incidentDate" />
        </label>
        @if (showError('incidentDate')) {
          <p class="error">Enter the date of the incident.</p>
        }

        <label>
          Description
          <textarea formControlName="description" rows="5" maxlength="2000"></textarea>
        </label>
        @if (showError('description')) {
          <p class="error">Describe what happened.</p>
        }

        @for (line of serverError(); track $index) {
          <p class="error">{{ line }}</p>
        }

        <button type="submit" [disabled]="sending()">
          {{ sending() ? 'Submitting…' : 'Submit Claim' }}
        </button>
      </form>
    }
  `,
  styles: `
    form {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      max-width: 32rem;
    }
    label {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    p.error,
    p.hint {
      margin: 0;
    }
    button {
      align-self: flex-start;
    }
  `,
})
export class SubmitClaim {
  private readonly api = inject(ClaimantApi);

  protected readonly markets = markets;
  protected readonly statusLabel = statusLabel;

  protected readonly form = inject(NonNullableFormBuilder).group({
    policyNumber: ['', [Validators.required, notBlank, Validators.maxLength(50)]],
    market: ['' as Market | '', Validators.required],
    claimType: ['' as ClaimType | '', Validators.required],
    incidentDate: ['', Validators.required],
    description: ['', [Validators.required, notBlank, Validators.maxLength(2000)]],
  });

  // The chosen Market's currency, so the Claimant sees it follows from the Market (D7).
  private readonly market = toSignal(this.form.controls.market.valueChanges, { initialValue: '' });
  protected readonly currency = computed(
    () => markets.find((m) => m.code === this.market())?.currency ?? '',
  );

  protected readonly sending = signal(false);
  /** Whether Submit has been pressed: field errors only show after that, not while typing. */
  private readonly tried = signal(false);
  protected readonly serverError = signal<string[]>([]);
  /** The new Claim, once the API has accepted it. */
  protected readonly submitted = signal<Claim | null>(null);

  protected showError(field: keyof typeof this.form.controls): boolean {
    return this.tried() && this.form.controls[field].invalid;
  }

  protected submit(): void {
    this.tried.set(true);
    this.serverError.set([]);
    if (this.form.invalid) {
      return;
    }
    this.sending.set(true);
    // The validators above guarantee market and claimType are chosen, so the casts are safe.
    const value = this.form.getRawValue();
    this.api
      .submit({ ...value, market: value.market as Market, claimType: value.claimType as ClaimType })
      .subscribe({
        next: (claim) => this.submitted.set(claim),
        error: (err: HttpErrorResponse) => {
          this.serverError.set(errorLines(err));
          this.sending.set(false);
        },
      });
  }
}
