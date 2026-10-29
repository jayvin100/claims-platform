import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CurrentUser } from './current-user';
import { DemoUser, Role, homeFor } from './demo-user';
import { UsersApi } from './users-api';

/** There is no login (ADR-0006): pick a seeded Claimant or Officer and use the app as them. */
@Component({
  selector: 'app-role-picker',
  template: `
    <h1>Who are you?</h1>
    <p>There is no login in this demo. Pick a user to act as; you can switch at any time.</p>

    @if (error()) {
      <p class="error">{{ error() }}</p>
    } @else if (users() === null) {
      <p>Loading users…</p>
    } @else {
      <h2>Claimants</h2>
      <ul>
        @for (user of claimants(); track user.id) {
          <li>
            <button type="button" (click)="choose(user)">{{ user.name }}</button>
          </li>
        }
      </ul>

      <h2>Officers</h2>
      <ul>
        @for (user of officers(); track user.id) {
          <li>
            <button type="button" (click)="choose(user)">{{ user.name }}</button>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    ul {
      list-style: none;
      padding: 0;
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
  `,
})
export class RolePicker {
  private readonly currentUser = inject(CurrentUser);
  private readonly router = inject(Router);

  /** null until the API answers. */
  protected readonly users = signal<DemoUser[] | null>(null);
  protected readonly error = signal('');
  // computed() re-runs only when users() changes.
  protected readonly claimants = computed(() => this.usersWithRole('Claimant'));
  protected readonly officers = computed(() => this.usersWithRole('Officer'));

  constructor() {
    // One request that completes, so there is nothing to unsubscribe from.
    inject(UsersApi)
      .list()
      .subscribe({
        next: (users) => this.users.set(users),
        error: () => this.error.set('Could not load the demo users. Is the API running?'),
      });
  }

  protected choose(user: DemoUser): void {
    this.currentUser.choose(user);
    this.router.navigateByUrl(homeFor(user.role));
  }

  private usersWithRole(role: Role): DemoUser[] {
    return (this.users() ?? []).filter((u) => u.role === role);
  }
}
