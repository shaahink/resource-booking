import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client';
import { SaveUserRequest, User, UserRole } from '../../core/models';

interface UserDraft {
  id: string | null;
  name: string;
  email: string;
  role: UserRole;
}

@Component({
  selector: 'app-users-admin',
  imports: [FormsModule],
  templateUrl: './users-admin.component.html',
  styleUrl: './users-admin.component.scss',
})
export class UsersAdminComponent {
  private readonly api = inject(ApiClient);

  protected readonly users = signal<User[]>([]);
  protected readonly draft = signal<UserDraft | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly roles: UserRole[] = ['User', 'Admin'];

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getUsers().subscribe((users) => this.users.set(users));
  }

  protected startNew(): void {
    this.error.set(null);
    this.draft.set({ id: null, name: '', email: '', role: 'User' });
  }

  protected startEdit(user: User): void {
    this.error.set(null);
    this.draft.set({ id: user.id, name: user.name, email: user.email, role: user.role });
  }

  protected save(): void {
    const draft = this.draft();
    if (!draft || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);

    const request: SaveUserRequest = {
      name: draft.name.trim(),
      email: draft.email.trim(),
      role: draft.role,
    };
    const call = draft.id ? this.api.updateUser(draft.id, request) : this.api.createUser(request);

    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.draft.set(null);
        this.load();
      },
      error: (err: { error?: { title?: string } }) => {
        this.saving.set(false);
        this.error.set(err.error?.title ?? 'Could not save the user.');
      },
    });
  }
}
