import { Component, inject, signal } from '@angular/core';
import { ApiClient } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import { User } from '../../core/models';

/**
 * Demo-mode login: pick a seeded user. Roles are still enforced server-side on every
 * endpoint; swapping this picker for a credential/SSO flow touches only the auth feature.
 */
@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly api = inject(ApiClient);
  protected readonly auth = inject(AuthService);

  protected readonly users = signal<User[]>([]);

  constructor() {
    this.api.getLoginUsers().subscribe((users) => this.users.set(users));
  }

  protected pick(user: User): void {
    this.auth.login(user.id);
  }
}
