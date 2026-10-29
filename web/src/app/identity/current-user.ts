import { Injectable, signal } from '@angular/core';
import { DemoUser } from './demo-user';

const storageKey = 'claims.demoUser';

/**
 * The Demo User the app is acting as. It is a signal, so the header, guards and interceptor
 * always read the current value. It is saved to localStorage so a reload keeps the choice
 * until the user switches.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUser {
  private readonly current = signal<DemoUser | null>(load());

  /** Read-only outside this service: only choose() and clear() change it. */
  readonly user = this.current.asReadonly();

  choose(user: DemoUser): void {
    localStorage.setItem(storageKey, JSON.stringify(user));
    this.current.set(user);
  }

  clear(): void {
    localStorage.removeItem(storageKey);
    this.current.set(null);
  }
}

function load(): DemoUser | null {
  try {
    return JSON.parse(localStorage.getItem(storageKey) ?? 'null');
  } catch {
    return null; // a damaged value means "nobody chosen", so the role picker shows again
  }
}
