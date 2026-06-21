import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LeagueContextService } from '../../core/league-context.service';

@Component({
  selector: 'app-league-picker',
  imports: [FormsModule, RouterLink],
  templateUrl: './league-picker.component.html',
  styleUrl: './league-picker.component.scss',
})
export class LeaguePickerComponent {
  private ctx = inject(LeagueContextService);

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
}
