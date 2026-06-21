import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LeagueContextService } from '../../core/league-context.service';
import { League, LeagueAdmin } from '../../core/models';

@Component({
  selector: 'app-super-admin',
  imports: [FormsModule, RouterLink],
  templateUrl: './super-admin.component.html',
  styleUrl: './super-admin.component.scss',
})
export class SuperAdminComponent {
  private api = inject(ApiService);
  private auth = inject(AuthService);
  private ctx = inject(LeagueContextService);

  isSuperAdmin = this.auth.isSuperAdmin;

  // login
  username = signal('');
  password = signal('');
  loginError = signal<string | null>(null);
  busy = signal(false);

  // dati
  leagues = signal<League[]>([]);
  error = signal<string | null>(null);

  // nuova lega
  newSlug = signal('');
  newName = signal('');
  newTitle = signal('');

  // modifica dettagli lega (inline)
  editingLeagueId = signal<number | null>(null);
  editName = signal('');
  editTitle = signal('');

  // gestione admin per lega espansa
  expandedLeagueId = signal<number | null>(null);
  admins = signal<LeagueAdmin[]>([]);
  adminUsername = signal('');
  adminPassword = signal('');
  adminError = signal<string | null>(null);

  // modifica admin esistente (inline)
  editingAdminId = signal<number | null>(null);
  editAdminUsername = signal('');
  editAdminPassword = signal('');

  expandedLeague = computed(() =>
    this.leagues().find((l) => l.id === this.expandedLeagueId()) ?? null,
  );

  constructor() {
    this.ctx.setSlug(null); // niente header lega: login senza slug = super-admin
    if (this.isSuperAdmin()) this.reload();
  }

  doLogin(): void {
    this.loginError.set(null);
    this.busy.set(true);
    this.auth.login({ username: this.username(), password: this.password() }).subscribe({
      next: () => {
        this.busy.set(false);
        this.reload();
      },
      error: (err) => {
        this.loginError.set(err.status === 401 ? 'Credenziali non valide o non super-admin.' : 'Errore di accesso.');
        this.busy.set(false);
      },
    });
  }

  logout(): void {
    this.auth.logout();
    this.leagues.set([]);
  }

  reload(): void {
    this.api.getAllLeagues().subscribe({
      next: (ls) => this.leagues.set(ls),
      error: () => this.error.set('Impossibile caricare le leghe.'),
    });
  }

  createLeague(): void {
    this.error.set(null);
    this.api.createLeague({
      slug: this.newSlug(),
      name: this.newName(),
      title: this.newTitle() || null,
    }).subscribe({
      next: () => {
        this.newSlug.set('');
        this.newName.set('');
        this.newTitle.set('');
        this.ctx.loadLeagues(); // aggiorna anche la cache pubblica (selettore/picker)
        this.reload();
      },
      error: (err) => this.error.set(err.error?.error ?? 'Errore creazione lega.'),
    });
  }

  startEditLeague(l: League): void {
    this.error.set(null);
    this.editingLeagueId.set(l.id);
    this.editName.set(l.name);
    this.editTitle.set(l.title ?? '');
  }

  cancelEditLeague(): void {
    this.editingLeagueId.set(null);
  }

  saveLeague(l: League): void {
    this.error.set(null);
    this.api.updateLeague(l.id, {
      name: this.editName(),
      title: this.editTitle() || null,
    }).subscribe({
      next: () => {
        this.editingLeagueId.set(null);
        this.ctx.loadLeagues();
        this.reload();
      },
      error: (err) => this.error.set(err.error?.error ?? 'Errore aggiornamento lega.'),
    });
  }

  toggleActive(l: League): void {
    this.api.updateLeague(l.id, { isActive: !l.isActive }).subscribe({
      next: () => {
        this.ctx.loadLeagues();
        this.reload();
      },
      error: (err) => this.error.set(err.error?.error ?? 'Errore aggiornamento lega.'),
    });
  }

  manageAdmins(l: League): void {
    if (this.expandedLeagueId() === l.id) {
      this.expandedLeagueId.set(null);
      return;
    }
    this.expandedLeagueId.set(l.id);
    this.adminError.set(null);
    this.adminUsername.set('');
    this.adminPassword.set('');
    this.api.getLeagueAdmins(l.id).subscribe({
      next: (a) => this.admins.set(a),
      error: () => this.adminError.set('Impossibile caricare gli admin.'),
    });
  }

  createAdmin(): void {
    const id = this.expandedLeagueId();
    if (id == null) return;
    this.adminError.set(null);
    this.api.createLeagueAdmin(id, {
      username: this.adminUsername(),
      password: this.adminPassword(),
    }).subscribe({
      next: () => {
        this.adminUsername.set('');
        this.adminPassword.set('');
        this.reloadAdmins(id);
      },
      error: (err) => this.adminError.set(err.error?.error ?? 'Errore creazione admin.'),
    });
  }

  private reloadAdmins(leagueId: number): void {
    this.api.getLeagueAdmins(leagueId).subscribe((a) => this.admins.set(a));
  }

  startEditAdmin(a: LeagueAdmin): void {
    this.adminError.set(null);
    this.editingAdminId.set(a.id);
    this.editAdminUsername.set(a.username);
    this.editAdminPassword.set('');
  }

  cancelEditAdmin(): void {
    this.editingAdminId.set(null);
  }

  saveAdmin(a: LeagueAdmin): void {
    const id = this.expandedLeagueId();
    if (id == null) return;
    this.adminError.set(null);
    // Password vuota = invariata (solo rename).
    this.api.updateLeagueAdmin(id, a.id, {
      username: this.editAdminUsername(),
      password: this.editAdminPassword() || null,
    }).subscribe({
      next: () => {
        this.editingAdminId.set(null);
        this.reloadAdmins(id);
      },
      error: (err) => this.adminError.set(err.error?.error ?? 'Errore aggiornamento admin.'),
    });
  }

  deleteAdmin(a: LeagueAdmin): void {
    const id = this.expandedLeagueId();
    if (id == null) return;
    if (!confirm(`Eliminare l'admin "${a.username}"?`)) return;
    this.adminError.set(null);
    this.api.deleteLeagueAdmin(id, a.id).subscribe({
      next: () => this.reloadAdmins(id),
      error: (err) => this.adminError.set(err.error?.error ?? 'Errore eliminazione admin.'),
    });
  }
}
