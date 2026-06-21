import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { Season } from '../../core/models';

@Component({
  selector: 'app-impostazioni',
  imports: [FormsModule],
  templateUrl: './impostazioni.component.html',
  styleUrl: './impostazioni.component.scss',
})
export class ImpostazioniComponent {
  private api = inject(ApiService);
  private ctx = inject(LeagueContextService);

  season = signal<Season | null>(null);
  totalStages = signal(8);
  countingStages = signal(8);
  busy = signal(false);
  error = signal<string | null>(null);
  ok = signal<string | null>(null);

  // --- logo lega ---
  current = this.ctx.current;
  logoBusy = signal(false);
  logoError = signal<string | null>(null);
  logoSrc = computed(() => {
    const slug = this.ctx.slug();
    return slug && this.current()?.hasLogo ? this.api.logoUrl(slug, this.ctx.logoVersion()) : null;
  });

  constructor() {
    this.api.getSeason().subscribe((s) => {
      this.season.set(s);
      this.totalStages.set(s.totalStages);
      this.countingStages.set(s.countingStages);
    });
  }

  onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // permette di riselezionare lo stesso file
    if (!file) return;

    this.logoError.set(null);
    this.logoBusy.set(true);
    this.api.uploadLogo(file).subscribe({
      next: () => this.afterLogoChange(),
      error: (err) => {
        this.logoError.set(err.error?.error ?? 'Errore nel caricamento del logo.');
        this.logoBusy.set(false);
      },
    });
  }

  removeLogo(): void {
    this.logoError.set(null);
    this.logoBusy.set(true);
    this.api.deleteLogo().subscribe({
      next: () => this.afterLogoChange(),
      error: (err) => {
        this.logoError.set(err.error?.error ?? 'Errore nella rimozione del logo.');
        this.logoBusy.set(false);
      },
    });
  }

  // Ricarica l'elenco leghe (aggiorna hasLogo) e busta la cache degli <img>.
  private afterLogoChange(): void {
    this.ctx.loadLeagues();
    this.ctx.bumpLogoVersion();
    this.logoBusy.set(false);
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
