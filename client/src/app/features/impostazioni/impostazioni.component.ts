import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { Season } from '../../core/models';

@Component({
  selector: 'app-impostazioni',
  imports: [FormsModule],
  templateUrl: './impostazioni.component.html',
  styleUrl: './impostazioni.component.scss',
})
export class ImpostazioniComponent {
  private api = inject(ApiService);

  season = signal<Season | null>(null);
  totalStages = signal(8);
  countingStages = signal(8);
  busy = signal(false);
  error = signal<string | null>(null);
  ok = signal<string | null>(null);

  constructor() {
    this.api.getSeason().subscribe((s) => {
      this.season.set(s);
      this.totalStages.set(s.totalStages);
      this.countingStages.set(s.countingStages);
    });
  }

  save(): void {
    this.error.set(null);
    this.ok.set(null);
    this.busy.set(true);
    this.api.updateSeason({ totalStages: this.totalStages(), countingStages: this.countingStages() }).subscribe({
      next: (s) => {
        this.season.set(s);
        this.ok.set('Configurazione salvata.');
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.error ?? 'Errore nel salvataggio.');
        this.busy.set(false);
      },
    });
  }
}
