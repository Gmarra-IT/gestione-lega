import { NgTemplateOutlet } from '@angular/common';
import { Component, HostListener, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
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

  // Drawer mobile.
  menuOpen = signal(false);

  // URL del logo lega (con cache-busting), oppure null → fallback avatar iniziali.
  logoSrc = computed(() =>
    this.current()?.hasLogo ? this.api.logoUrl(this.slug(), this.ctx.logoVersion()) : null,
  );

  // Avatar fallback: iniziali + colore derivato dal nome.
  initials = computed(() => {
    const name = this.current()?.name ?? this.brand();
    const parts = name.trim().split(/\s+/).filter(Boolean);
    const letters = parts.length >= 2 ? parts[0][0] + parts[1][0] : name.slice(0, 2);
    return letters.toUpperCase();
  });
  avatarColor = computed(() => {
    const s = this.slug() || this.brand();
    let h = 0;
    for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) % 360;
    return `hsl(${h} 55% 45%)`;
  });

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

  switchTo(slug: string): void {
    if (slug) this.router.navigate(['/', slug, 'classifica']);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
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
