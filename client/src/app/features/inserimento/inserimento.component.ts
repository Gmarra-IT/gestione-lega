import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { PlayerSelection, Season, Stage, StageResult } from '../../core/models';
import { PlayerPickerComponent } from '../../core/player-picker.component';
import * as scoring from '../../core/scoring';
import { ImportazioneComponent } from '../importazione/importazione.component';

@Component({
  selector: 'app-inserimento',
  imports: [FormsModule, ImportazioneComponent, PlayerPickerComponent],
  templateUrl: './inserimento.component.html',
  styleUrl: './inserimento.component.scss',
})
export class InserimentoComponent {
  private api = inject(ApiService);
  private ctx = inject(LeagueContextService);

  // Tab attiva: inserimento manuale o import PDF.
  tab = signal<'manuale' | 'import'>('manuale');

  season = signal<Season | null>(null);
  stages = signal<Stage[]>([]);
  stageResults = signal<StageResult[]>([]);

  // form state
  stageNumber = signal<number | null>(null);
  // Giocatore: un unico picker che cerca un esistente o crea un nuovo nome.
  selection = signal<PlayerSelection>(null);
  // Risultato in W/D/L: i match points sono derivati dalla ScoringRule.
  wins = signal<number>(0);
  draws = signal<number>(0);
  losses = signal<number>(0);
  position = signal<number | null>(null);

  // count of the selected player's OTHER stages with points > 0 (for bonus preview)
  priorParticipations = signal(0);

  busy = signal(false);
  error = signal<string | null>(null);
  ok = signal<string | null>(null);

  // live preview (mirror di ScoringService, parametrizzato sulla regola della stagione)
  private rule = computed(() => this.season()?.scoringRule ?? null);
  matchPoints = computed(() => {
    const r = this.rule();
    return r ? scoring.matchPoints(this.wins(), this.draws(), this.losses(), r) : 0;
  });
  previewScoreBonus = computed(() => {
    const r = this.rule();
    return r ? scoring.scoreBonus(this.matchPoints(), r) : 0;
  });
  previewPositionBonus = computed(() => {
    const r = this.rule();
    return r ? scoring.positionBonus(this.position(), r) : 0;
  });
  previewParticipation = computed(() => {
    const r = this.rule();
    return r ? scoring.participationPoints(this.priorParticipations() + 1, r) : 0;
  });
  previewTotal = computed(() =>
    this.matchPoints() + this.previewScoreBonus() + this.previewPositionBonus() + this.previewParticipation());

  constructor() {
    // Ricarica i dati al primo avvio e a ogni cambio di stagione selezionata.
    effect(() => {
      this.ctx.selectedSeasonId();
      this.load();
    });

    // reload the selected stage's results + recompute prior participations on relevant changes
    effect(() => {
      const n = this.stageNumber();
      if (n !== null) this.api.getStageResults(n).subscribe((r) => this.stageResults.set(r));
    });
    effect(() => {
      this.recomputePrior(this.selection(), this.stageNumber());
    });
  }

  private load(): void {
    this.api.getSeason().subscribe((s) => this.season.set(s));
    // Reset selezione tappa: la stagione potrebbe avere tappe diverse → riseleziona la prima.
    this.stageNumber.set(null);
    this.api.getStages().subscribe((st) => {
      this.stages.set(st);
      if (st.length) this.stageNumber.set(st[0].number);
    });
  }

  private recomputePrior(selection: PlayerSelection, stageNumber: number | null): void {
    if (selection?.kind !== 'existing') {
      this.priorParticipations.set(0);
      return;
    }
    this.api.getProgression(selection.id).subscribe((prog) => {
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
        if (n !== null) this.api.getStageResults(n).subscribe((res) => this.stageResults.set(res));
        this.recomputePrior(this.selection(), n);
      },
      error: (err) => this.error.set(err.error?.error ?? 'Errore nell\'eliminazione.'),
    });
  }

  submit(): void {
    this.error.set(null);
    this.ok.set(null);
    const n = this.stageNumber();
    if (n === null) { this.error.set('Seleziona una tappa.'); return; }
    const sel = this.selection();
    if (sel === null) { this.error.set('Seleziona o crea un giocatore.'); return; }

    this.busy.set(true);
    this.api.upsertResult({
      stageNumber: n,
      playerId: sel.kind === 'existing' ? sel.id : null,
      newPlayerName: sel.kind === 'new' ? sel.name.trim() : null,
      wins: this.wins(),
      draws: this.draws(),
      losses: this.losses(),
      position: this.position(),
    }).subscribe({
      next: (res) => {
        this.ok.set(`${res.displayName}: ${res.matchPoints} + ${res.scoreBonus} + ${res.positionBonus} + ${res.participationPoints} = ${res.totalPoints}`);
        this.busy.set(false);
        this.selection.set(null);
        this.api.getStageResults(n).subscribe((r) => this.stageResults.set(r));
        this.recomputePrior(this.selection(), n);
      },
      error: (err) => {
        this.error.set(err.error?.error ?? 'Errore nel salvataggio.');
        this.busy.set(false);
      },
    });
  }
}
