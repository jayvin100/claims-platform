import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { CurrentUser } from './identity/current-user';

/** The shell: a header with who you are and "Switch user", above whichever screen is open. */
@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  template: `
    <header>
      <strong>Claims</strong>
      <!-- Shown on every screen once a user is chosen; the role picker itself is the switch. -->
      @if (currentUser.user(); as user) {
        <span>{{ user.name }} ({{ user.role }})</span>
        <button type="button" (click)="switchUser()">Switch user</button>
      }
    </header>
    <main>
      <router-outlet />
    </main>
  `,
  styles: `
    header {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 0.75rem 1.5rem;
      border-bottom: 1px solid #ccc;
    }
    header span {
      margin-left: auto;
    }
    main {
      padding: 1.5rem;
    }
  `,
})
export class App {
  protected readonly currentUser = inject(CurrentUser);
  private readonly router = inject(Router);

  protected switchUser(): void {
    this.currentUser.clear();
    this.router.navigateByUrl('/');
  }
}
