import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { apiBaseUrl } from '../api';
import { CurrentUser } from './current-user';

/**
 * Adds the chosen Demo User's id and role to every API call (ADR-0006). This is the only place
 * the headers are set, so services never deal with identity. The API decides what the user
 * may see and do; the headers only say who is asking.
 */
export const identityInterceptor: HttpInterceptorFn = (req, next) => {
  const user = inject(CurrentUser).user();
  // Only our API gets the headers; before a user is chosen there is nothing to add.
  if (!user || !isApiRequest(req.url)) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { 'X-User-Id': user.id, 'X-Role': user.role } }));
};

// Compare whole origins (scheme, host, port), not string prefixes: a prefix check would let
// http://localhost:51690 pass as http://localhost:5169. Relative URLs resolve against this page.
function isApiRequest(url: string): boolean {
  return new URL(url, location.href).origin === new URL(apiBaseUrl).origin;
}
