import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CurrentUser } from './current-user';
import { Role, homeFor } from './demo-user';

/**
 * Builds the guard for one role. With no user chosen, it sends you to the role picker; with the
 * other role, to your own home screen. Guards only hide screens: the API still enforces every rule.
 */
function requireRole(role: Role): CanActivateFn {
  return () => {
    const user = inject(CurrentUser).user();
    if (user?.role === role) {
      return true;
    }
    // Returning a UrlTree tells the router to go there instead.
    return inject(Router).parseUrl(user ? homeFor(user.role) : '/');
  };
}

export const claimantGuard = requireRole('Claimant');
export const officerGuard = requireRole('Officer');

/** The role picker is only for choosing: a remembered user goes straight to their home screen. */
export const noUserGuard: CanActivateFn = () => {
  const user = inject(CurrentUser).user();
  return user ? inject(Router).parseUrl(homeFor(user.role)) : true;
};
