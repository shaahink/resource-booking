import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import { SaveUserRequest, User, UserRole } from '../../core/models';
import { matchesQuery } from '../../core/search';
import { SearchBoxComponent } from '../../shared/search-box.component';

interface UserDraft {
  id: string | null;
  name: string;
  email: string;
  role: UserRole;
  isActive: boolean;
}

/** Shape check only — the server validates properly; this just catches obvious typos before a round-trip. */
const EMAIL_SHAPE = /^\S+@\S+\.\S+$/;

@Component({
  selector: 'app-users-admin',
  imports: [DatePipe, FormsModule, SearchBoxComponent],
  templateUrl: './users-admin.component.html',
  styleUrl: './users-admin.component.scss',
})
export class UsersAdminComponent {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthService);

  protected readonly users = signal<User[]>([]);
  protected readonly search = signal('');
  protected readonly draft = signal<UserDraft | null>(null);
  protected readonly saving = signal(false);
  protected readonly togglingId = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly roles: UserRole[] = ['User', 'Admin'];

  protected readonly visible = computed(() =>
    this.users().filter((u) => matchesQuery(this.search(), u.name, u.email, u.role)),
  );

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getUsers().subscribe((users) => this.users.set(users));
  }

  protected isSelf(user: User): boolean {
    return user.id === this.auth.currentUser()?.id;
  }

  protected emailLooksValid(email: string): boolean {
    return EMAIL_SHAPE.test(email.trim());
  }

  protected draftValid(draft: UserDraft): boolean {
    return draft.name.trim().length > 0 && this.emailLooksValid(draft.email);
  }

  protected startNew(): void {
    this.error.set(null);
    this.draft.set({ id: null, name: '', email: '', role: 'User', isActive: true });
  }

  protected startEdit(user: User): void {
    this.error.set(null);
    this.draft.set({ id: user.id, name: user.name, email: user.email, role: user.role, isActive: user.isActive });
  }

  /** Disabled users keep their history but drop off the login picker and cannot sign in. */
  protected toggleActive(user: User): void {
    if (this.togglingId() || this.isSelf(user)) return;
    if (user.isActive && !confirm(`Disable ${user.name}? They will no longer be able to sign in.`)) return;

    this.togglingId.set(user.id);
    this.error.set(null);
    const request: SaveUserRequest = { name: user.name, email: user.email, role: user.role, isActive: !user.isActive };
    this.api.updateUser(user.id, request).subscribe({
      next: () => {
        this.togglingId.set(null);
        this.load();
      },
      error: (err: { error?: { title?: string } }) => {
        this.togglingId.set(null);
        this.error.set(err.error?.title ?? 'Could not update the user.');
      },
    });
  }

  protected save(): void {
    const draft = this.draft();
    if (!draft || this.saving() || !this.draftValid(draft)) return;
    this.saving.set(true);
    this.error.set(null);

    const request: SaveUserRequest = {
      name: draft.name.trim(),
      email: draft.email.trim(),
      role: draft.role,
      isActive: draft.isActive,
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
