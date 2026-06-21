import { NgTemplateOutlet } from '@angular/common';
import { Component, HostListener, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { leagueAvatarColor, leagueInitials } from '../../core/league-avatar';
import { LeagueContextService, RESERVED_SLUGS } from '../../core/league-context.service';

@Component({
  selector: 'app-league-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgTemplateOutlet],
  templateUrl: './league-shell.component.html',
  styleUrl: './league-shell.component.scss',
})
export class LeagueShellComponent {
  // Bound dal route param :slug (withComponentInputBinding).
  slug = input.required<string>();

  private ctx = inject(LeagueContextService);
  private auth = inject(AuthService);
  private api = inject(ApiService);
  private router = inject(Router);

  isAdmin = this.auth.isAdmin;
  isSuperAdmin = this.auth.isSuperAdmin;
  brand = this.ctx.brand;
  leagues = this.ctx.leagues;
  current = this.ctx.current;
  currentSlug = this.ctx.slug;

  // Selettore stagione.
  seasons = this.ctx.seasons;
  currentSeasonId = computed(() => this.ctx.currentSeason()?.id ?? null);

  // Drawer mobile.
  menuOpen = signal(false);

  // URL del logo lega (con cache-busting), oppure null → fallback avatar iniziali.
  logoSrc = computed(() =>
    this.current()?.hasLogo ? this.api.logoUrl(this.slug(), this.ctx.logoVersion()) : null,
  );

  // Avatar fallback condiviso (vedi core/league-avatar).
  initials = computed(() => leagueInitials(this.current()?.name ?? this.brand()));
  avatarColor = computed(() => leagueAvatarColor(this.slug() || this.brand()));

  constructor() {
    // Tiene allineato il contesto allo slug della route e valida lo slug.
    effect(() => {
      const s = this.slug();
      this.ctx.setSlug(s);
      if (RESERVED_SLUGS.includes(s)) {
        this.router.navigateByUrl('/');
        return;
      }
      // Se l'elenco è caricato e lo slug non esiste → torna alla pagina-base.
      if (this.leagues().length > 0 && !this.ctx.isKnownSlug(s)) {
        this.router.navigateByUrl('/');
      }
    });

    if (this.ctx.leagues().length === 0) this.ctx.loadLeagues();
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  onSeasonChange(event: Event): void {
    const id = +(event.target as HTMLSelectElement).value;
    const season = this.seasons().find((s) => s.id === id);
    // Stagione attiva → null (segue l'attiva); altrimenti fissa la selezione.
    this.ctx.setSelectedSeasonId(season?.isActive ? null : id);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenu();
  }

  logout(): void {
    this.closeMenu();
    this.auth.logout();
    this.router.navigate(['/', this.slug(), 'classifica']);
  }
}
