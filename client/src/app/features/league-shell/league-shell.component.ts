import { Component, computed, effect, inject, input } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { LeagueContextService, RESERVED_SLUGS } from '../../core/league-context.service';

@Component({
  selector: 'app-league-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './league-shell.component.html',
  styleUrl: './league-shell.component.scss',
})
export class LeagueShellComponent {
  // Bound dal route param :slug (withComponentInputBinding).
  slug = input.required<string>();

  private ctx = inject(LeagueContextService);
  private auth = inject(AuthService);
  private router = inject(Router);

  isAdmin = this.auth.isAdmin;
  brand = this.ctx.brand;
  leagues = this.ctx.leagues;
  currentSlug = this.ctx.slug;

  // Altre leghe per il selettore.
  others = computed(() => this.leagues().filter((l) => l.slug !== this.currentSlug()));

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

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/', this.slug(), 'classifica']);
  }
}
