import { Routes } from '@angular/router';
import { MyClaims } from './claimant/my-claims';
import { RolePicker } from './identity/role-picker';
import { claimantGuard, noUserGuard, officerGuard } from './identity/role-guards';
import { Queue } from './officer/queue';

// Every screen except the role picker sits behind the guard for its role.
export const routes: Routes = [
  { path: '', component: RolePicker, canActivate: [noUserGuard], title: 'Choose a user' },
  { path: 'my-claims', component: MyClaims, canActivate: [claimantGuard], title: 'My Claims' },
  { path: 'queue', component: Queue, canActivate: [officerGuard], title: 'Queue' },
  { path: '**', redirectTo: '' },
];
