import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { compressImage } from '../../core/image-compress';
import { ParticipationTier, PositionBonus, ScoreBonus, Season } from '../../core/models';

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

  // --- regola di scoring ---
  pointsPerWin = signal(3);
  pointsPerDraw = signal(1);
  pointsPerLoss = signal(0);
  scoreBonuses = signal<ScoreBonus[]>([]);
  positionBonuses = signal<PositionBonus[]>([]);
  participationTiers = signal<ParticipationTier[]>([]);
  ruleBusy = signal(false);
  ruleError = signal<string | null>(null);
  ruleOk = signal<string | null>(null);

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
        this.loadRule(s);
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

  // --- regola di scoring ---
  private loadRule(s: Season): void {
    const r = s.scoringRule;
    this.pointsPerWin.set(r.pointsPerWin);
    this.pointsPerDraw.set(r.pointsPerDraw);
    this.pointsPerLoss.set(r.pointsPerLoss);
    // copie modificabili (no mutazione del DTO della season)
    this.scoreBonuses.set(r.scoreBonuses.map((b) => ({ ...b })));
    this.positionBonuses.set(r.positionBonuses.map((b) => ({ ...b })));
    this.participationTiers.set(r.participationTiers.map((t) => ({ ...t })));
  }

  addScoreBonus(): void {
    this.scoreBonuses.update((l) => [...l, { fromMatchPoints: 0, points: 0 }]);
  }
  removeScoreBonus(i: number): void {
    this.scoreBonuses.update((l) => l.filter((_, j) => j !== i));
  }
  setScoreBonus(i: number, field: keyof ScoreBonus, value: number): void {
    this.scoreBonuses.update((l) => l.map((b, j) => (j === i ? { ...b, [field]: value } : b)));
  }

  addPositionBonus(): void {
    this.positionBonuses.update((l) => [...l, { position: l.length + 1, points: 0 }]);
  }
  removePositionBonus(i: number): void {
    this.positionBonuses.update((l) => l.filter((_, j) => j !== i));
  }
  setPositionBonus(i: number, field: keyof PositionBonus, value: number): void {
    this.positionBonuses.update((l) => l.map((b, j) => (j === i ? { ...b, [field]: value } : b)));
  }

  addParticipationTier(): void {
    this.participationTiers.update((l) => [...l, { fromTournament: 1, pointsPerParticipation: 0 }]);
  }
  removeParticipationTier(i: number): void {
    this.participationTiers.update((l) => l.filter((_, j) => j !== i));
  }
  setParticipationTier(i: number, field: keyof ParticipationTier, value: number): void {
    this.participationTiers.update((l) => l.map((t, j) => (j === i ? { ...t, [field]: value } : t)));
  }

  saveRule(): void {
    this.ruleError.set(null);
    this.ruleOk.set(null);
    this.ruleBusy.set(true);
    // ordino le liste a soglia crescente per comodità (il server lo richiede).
    const scoringRule = {
      pointsPerWin: this.pointsPerWin(),
      pointsPerDraw: this.pointsPerDraw(),
      pointsPerLoss: this.pointsPerLoss(),
      positionBonuses: [...this.positionBonuses()].sort((a, b) => a.position - b.position),
      scoreBonuses: [...this.scoreBonuses()].sort((a, b) => a.fromMatchPoints - b.fromMatchPoints),
      participationTiers: [...this.participationTiers()].sort((a, b) => a.fromTournament - b.fromTournament),
    };
    this.api.updateScoringRule({ scoringRule }).subscribe({
      next: (s) => {
        this.season.set(s);
        this.loadRule(s);
        this.ruleOk.set('Regola di scoring salvata. Classifica ricalcolata.');
        this.ruleBusy.set(false);
      },
      error: (err) => {
        this.ruleError.set(err.error?.error ?? 'Errore nel salvataggio della regola.');
        this.ruleBusy.set(false);
      },
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
