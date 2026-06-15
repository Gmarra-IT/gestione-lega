import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { Season, Stage, StageResult, StandingRow } from '../../core/models';
import { bonusPartecipazione, bonusRisultato } from '../../core/scoring';

@Component({
  selector: 'app-inserimento',
  imports: [FormsModule],
  templateUrl: './inserimento.component.html',
  styleUrl: './inserimento.component.scss',
})
export class InserimentoComponent {
  private api = inject(ApiService);

  season = signal<Season | null>(null);
  stages = signal<Stage[]>([]);
  players = signal<StandingRow[]>([]);
  stageResults = signal<StageResult[]>([]);

  // form state
  stageNumber = signal<number | null>(null);
  mode = signal<'existing' | 'new'>('existing');
  playerId = signal<number | null>(null);
  newPlayerName = signal('');
  matchPoints = signal<number>(12);

  // count of the selected player's OTHER stages with points > 0 (for bonus preview)
  priorParticipations = signal(0);

  busy = signal(false);
  error = signal<string | null>(null);
  ok = signal<string | null>(null);

  // live bonus preview (mirrors ScoringService)
  previewBonusRisultato = computed(() => bonusRisultato(this.matchPoints()));
  previewBonusPartecipazione = computed(() => bonusPartecipazione(this.priorParticipations()));
  previewTotal = computed(() =>
    this.matchPoints() + this.previewBonusRisultato() + this.previewBonusPartecipazione());

  constructor() {
    this.api.getSeason().subscribe((s) => this.season.set(s));
    this.refreshPlayers();
    this.api.getStages().subscribe((st) => {
      this.stages.set(st);
      if (st.length && this.stageNumber() === null) this.stageNumber.set(st[0].number);
    });

    // reload the selected stage's results + recompute prior participations on relevant changes
    effect(() => {
      const n = this.stageNumber();
      if (n !== null) this.api.getStageResults(n).subscribe((r) => this.stageResults.set(r));
    });
    effect(() => {
      this.recomputePrior(this.mode(), this.playerId(), this.stageNumber());
    });
  }

  private refreshPlayers(): void {
    this.api.getStandings().subscribe((p) =>
      this.players.set([...p].sort((a, b) => a.displayName.localeCompare(b.displayName))));
  }

  private recomputePrior(mode: 'existing' | 'new', playerId: number | null, stageNumber: number | null): void {
    if (mode === 'new' || playerId === null) {
      this.priorParticipations.set(0);
      return;
    }
    this.api.getProgression(playerId).subscribe((prog) => {
      const participated = prog.points.filter(
        (pt) => pt.stageTotal !== null && pt.stageNumber !== stageNumber);
      this.priorParticipations.set(participated.length);
    });
  }

  deleteResult(r: StageResult): void {
    if (!confirm(`Eliminare il risultato di ${r.displayName}?`)) return;
    this.error.set(null);
    this.ok.set(null);
    const n = this.stageNumber();
    this.api.deleteResult(r.id).subscribe({
      next: () => {
        this.ok.set(`Risultato di ${r.displayName} eliminato.`);
        this.refreshPlayers();
        if (n !== null) this.api.getStageResults(n).subscribe((res) => this.stageResults.set(res));
        this.recomputePrior(this.mode(), this.playerId(), n);
      },
      error: (err) => this.error.set(err.error?.error ?? 'Errore nell\'eliminazione.'),
    });
  }

  submit(): void {
    this.error.set(null);
    this.ok.set(null);
    const n = this.stageNumber();
    if (n === null) { this.error.set('Seleziona una tappa.'); return; }

    this.busy.set(true);
    this.api.upsertResult({
      stageNumber: n,
      playerId: this.mode() === 'existing' ? this.playerId() : null,
      newPlayerName: this.mode() === 'new' ? this.newPlayerName().trim() : null,
      matchPoints: this.matchPoints(),
    }).subscribe({
      next: (res) => {
        this.ok.set(`${res.displayName}: ${res.matchPoints} + ${res.bonusRisultato} + ${res.bonusPartecipazione} = ${res.totalPoints}`);
        this.busy.set(false);
        this.newPlayerName.set('');
        this.refreshPlayers();
        this.api.getStageResults(n).subscribe((r) => this.stageResults.set(r));
        this.recomputePrior(this.mode(), this.playerId(), n);
      },
      error: (err) => {
        this.error.set(err.error?.error ?? 'Errore nel salvataggio.');
        this.busy.set(false);
      },
    });
  }
}
