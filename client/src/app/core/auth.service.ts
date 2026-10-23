import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { EMPTY, catchError, finalize, of, tap } from 'rxjs';
import { ApiClient } from './api-client';
import { User } from './models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);

  private readonly user = signal<User | null>(null);
  private readonly initialised = signal(false);

  readonly currentUser = this.user.asReadonly();
  readonly isAdmin = computed(() => this.user()?.role === 'Admin');
  readonly ready = this.initialised.asReadonly();

  /** Restore the session from the auth cookie on app start. Returned to the app
   *  initializer so route guards never run before the session is known. */
  init() {
    return this.api.me().pipe(
      catchError(() => of(null)),
      tap((user) => {
        this.user.set(user);
        this.initialised.set(true);
      }),
    );
  }

  login(userId: string): void {
    this.api.login(userId).subscribe((user) => {
      this.user.set(user);
      this.router.navigateByUrl('/resources');
    });
  }

  logout(): void {
    this.api
      .logout()
      .pipe(
        catchError(() => EMPTY),
        finalize(() => this.endSession()),
      )
      .subscribe();
  }

  /** Forget the signed-in user locally and return to the login page. */
  endSession(): void {
    this.user.set(null);
    this.router.navigateByUrl('/login');
  }
}
