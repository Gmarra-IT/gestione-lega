import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { compressImage } from '../../core/image-compress';
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

  // --- nuova stagione ---
  newSeasonName = signal('');
  newTotalStages = signal(12);
  newCountingStages = signal(8);
  createBusy = signal(false);
  createError = signal<string | null>(null);
  createOk = signal<string | null>(null);

  // --- logo lega ---
  current = this.ctx.current;
  logoBusy = signal(false);
  logoError = signal<string | null>(null);
  logoSrc = computed(() => {
    const slug = this.ctx.slug();
    return slug && this.current()?.hasLogo ? this.api.logoUrl(slug, this.ctx.logoVersion()) : null;
  });

  constructor() {
    // Ricarica al primo avvio e a ogni cambio di stagione selezionata.
    effect(() => {
      this.ctx.selectedSeasonId();
      this.api.getSeason().subscribe((s) => {
        this.season.set(s);
        this.totalStages.set(s.totalStages);
        this.countingStages.set(s.countingStages);
      });
    });
  }

  createSeason(): void {
    this.createError.set(null);
    this.createOk.set(null);
    const name = this.newSeasonName().trim();
    if (!name) { this.createError.set('Inserisci un nome per la stagione.'); return; }
    this.createBusy.set(true);
    this.api.createSeason({
      name,
      totalStages: this.newTotalStages(),
      countingStages: this.newCountingStages(),
    }).subscribe({
      next: (s) => {
        this.createBusy.set(false);
        this.newSeasonName.set('');
        this.createOk.set(`Stagione "${s.name}" creata e attivata.`);
        // Aggiorna l'elenco stagioni e torna alla stagione attiva (la nuova).
        this.ctx.loadSeasons();
        this.ctx.setSelectedSeasonId(null);
      },
      error: (err) => {
        this.createError.set(err.error?.error ?? 'Errore nella creazione della stagione.');
        this.createBusy.set(false);
      },
    });
  }

  async onLogoSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // permette di riselezionare lo stesso file
    if (!file) return;

    this.logoError.set(null);
    this.logoBusy.set(true);

    let upload: File;
    try {
      // Ridimensiona/comprime lato client: l'admin può caricare anche foto pesanti.
      upload = await compressImage(file, { maxSide: 512, quality: 0.85 });
    } catch {
      upload = file; // se la compressione fallisce, prova con l'originale
    }

    this.api.uploadLogo(upload).subscribe({
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
