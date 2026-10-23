import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/guards';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'resources' },
      {
        path: 'resources',
        loadComponent: () =>
          import('./features/booking/browse.component').then((m) => m.BrowseComponent),
      },
      {
        path: 'resources/:id',
        loadComponent: () =>
          import('./features/booking/resource-detail.component').then((m) => m.ResourceDetailComponent),
      },
      {
        path: 'my-bookings',
        loadComponent: () =>
          import('./features/booking/my-bookings.component').then((m) => m.MyBookingsComponent),
      },
      {
        path: 'admin',
        canActivate: [adminGuard],
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
          {
            path: 'dashboard',
            loadComponent: () =>
              import('./features/admin/dashboard.component').then((m) => m.DashboardComponent),
          },
          {
            path: 'resources',
            loadComponent: () =>
              import('./features/admin/resources-admin.component').then((m) => m.ResourcesAdminComponent),
          },
          {
            path: 'users',
            loadComponent: () =>
              import('./features/admin/users-admin.component').then((m) => m.UsersAdminComponent),
          },
          {
            path: 'history',
            loadComponent: () =>
              import('./features/admin/history.component').then((m) => m.HistoryComponent),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
