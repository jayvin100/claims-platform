export type Role = 'Claimant' | 'Officer';

/** A seeded Claimant or Officer the role picker can act as, as GET /api/users returns it. */
export interface DemoUser {
  id: string;
  name: string;
  role: Role;
}

/** The screen each role lands on after picking a user. */
export function homeFor(role: Role): string {
  return role === 'Claimant' ? '/my-claims' : '/queue';
}
