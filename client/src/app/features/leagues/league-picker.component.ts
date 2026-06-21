import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { leagueAvatarColor, leagueInitials } from '../../core/league-avatar';
import { LeagueContextService } from '../../core/league-context.service';
import { League } from '../../core/models';

@Component({
  selector: 'app-league-picker',
  imports: [FormsModule, RouterLink],
  templateUrl: './league-picker.component.html',
  styleUrl: './league-picker.component.scss',
})
export class LeaguePickerComponent {
  private ctx = inject(LeagueContextService);
  private api = inject(ApiService);

  filter = signal('');

  filtered = computed(() => {
    const q = this.filter().trim().toLowerCase();
    const leagues = this.ctx.leagues();
    if (!q) return leagues;
    return leagues.filter(
      (l) => l.name.toLowerCase().includes(q) || l.slug.toLowerCase().includes(q),
    );
  });

  constructor() {
    // La pagina-base non è dentro una lega: azzera il contesto e carica l'elenco.
    this.ctx.setSlug(null);
    this.ctx.loadLeagues();
  }

  // URL del logo lega (con cache-busting), oppure null → fallback avatar iniziali.
  logoSrc(l: League): string | null {
    return l.hasLogo ? this.api.logoUrl(l.slug, this.ctx.logoVersion()) : null;
  }

  // Avatar fallback condiviso (vedi core/league-avatar).
  initials(l: League): string {
    return leagueInitials(l.name);
  }

  avatarColor(l: League): string {
    return leagueAvatarColor(l.slug || l.name);
  }
}
