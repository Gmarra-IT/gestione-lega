import { Component, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { Season, StandingRow } from '../../core/models';

@Component({
  selector: 'app-classifica',
  templateUrl: './classifica.component.html',
  styleUrl: './classifica.component.scss',
})
export class ClassificaComponent {
  private api = inject(ApiService);

  season = signal<Season | null>(null);
  rows = signal<StandingRow[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  constructor() {
    this.api.getSeason().subscribe({ next: (s) => this.season.set(s) });
    this.api.getStandings().subscribe({
      next: (r) => { this.rows.set(r); this.loading.set(false); },
      error: () => { this.error.set('Errore nel caricamento della classifica.'); this.loading.set(false); },
    });
  }
}
