import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { apiBaseUrl } from '../api';
import { DemoUser } from './demo-user';

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  /** Demo only: the seeded users for the role picker. Needs no identity headers. */
  list() {
    return this.http.get<DemoUser[]>(`${apiBaseUrl}/api/users`);
  }
}
