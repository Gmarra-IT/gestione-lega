import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import {
  ImportCommitResponse, ImportPreviewResponse, Season, Stage, StandingRow,
} from '../../core/models';

interface RowState {
  name: string;          // parsed name (display + name for a new player)
  matchPoints: number;
  playerId: number | null; // target player; null = create new from name
  isNew: boolean;          // parser found no match
}

@Component({
  selector: 'app-importazione',
  imports: [FormsModule],
  templateUrl: './importazione.component.html',
  styleUrl: './importazione.component.scss',
})
export class ImportazioneComponent {
  private api = inject(ApiService);

  season = signal<Season | null>(null);
  players = signal<StandingRow[]>([]);
  stages = signal<Stage[]>([]);

  fileName = signal<string | null>(null);
  preview = signal<ImportPreviewResponse | null>(null);
  rows = signal<RowState[]>([]);

  stageNumber = signal<number | null>(null);
  stageName = signal<string | null>(null);
  eventDate = signal<string | null>(null);
  eventLinkId = signal<string | null>(null);
  overwrite = signal(false);

  // Live existence of the currently-selected stage (re-evaluates when stageNumber changes,
  // not frozen at the value detected during PDF upload).
  existingResultCount = computed(() => {
    const n = this.stageNumber();
    if (n === null) return 0;
    return this.stages().find((s) => s.number === n)?.resultCount ?? 0;
  });
  stageHasResults = computed(() => this.existingResultCount() > 0);

  busy = signal(false);
  error = signal<string | null>(null);
  done = signal<ImportCommitResponse | null>(null);

  constructor() {
    this.api.getSeason().subscribe((s) => this.season.set(s));
    this.api.getStandings().subscribe((p) =>
      this.players.set([...p].sort((a, b) => a.displayName.localeCompare(b.displayName))));
    this.api.getStages().subscribe((s) => this.stages.set(s));
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.reset();
    this.fileName.set(file.name);
    this.busy.set(true);
    this.api.importPdfPreview(file).subscribe({
      next: (p) => {
        this.preview.set(p);
        this.stageNumber.set(p.stageNumber);
        this.stageName.set(p.stageName);
        this.eventDate.set(p.eventDate);
        this.eventLinkId.set(p.eventLinkId);
        this.rows.set(p.rows.map((r) => ({
          name: r.name,
          matchPoints: r.matchPoints,
          playerId: r.matchedPlayerId,
          isNew: r.isNew,
        })));
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.error ?? 'Errore nella lettura del PDF.');
        this.busy.set(false);
      },
    });
    input.value = ''; // allow re-selecting the same file
  }

  setRowPlayer(index: number, value: number | 'new'): void {
    const playerId = value === 'new' ? null : +value;
    this.rows.update((rows) =>
      rows.map((r, i) => (i === index ? { ...r, playerId } : r)));
  }

  setRowPoints(index: number, value: number): void {
    this.rows.update((rows) =>
      rows.map((r, i) => (i === index ? { ...r, matchPoints: value } : r)));
  }

  matchedCount(): number {
    return this.rows().filter((r) => r.playerId !== null).length;
  }
  newCount(): number {
    return this.rows().filter((r) => r.playerId === null).length;
  }

  commit(): void {
    const n = this.stageNumber();
    if (n === null) { this.error.set('Numero tappa mancante.'); return; }
    this.error.set(null);
    this.done.set(null);
    this.busy.set(true);

    this.api.importCommit({
      stageNumber: n,
      stageName: this.stageName(),
      eventDate: this.eventDate(),
      eventLinkId: this.eventLinkId(),
      overwrite: this.overwrite(),
      rows: this.rows().map((r) => ({
        name: r.name,
        matchPoints: r.matchPoints,
        playerId: r.playerId,
      })),
    }).subscribe({
      next: (res) => {
        this.done.set(res);
        this.busy.set(false);
        this.preview.set(null);
        this.rows.set([]);
        // refresh players + stages for next import
        this.api.getStandings().subscribe((p) =>
          this.players.set([...p].sort((a, b) => a.displayName.localeCompare(b.displayName))));
        this.api.getStages().subscribe((s) => this.stages.set(s));
      },
      error: (err) => {
        this.error.set(err.error?.error ?? 'Errore nel commit.');
        this.busy.set(false);
      },
    });
  }

  private reset(): void {
    this.error.set(null);
    this.done.set(null);
    this.preview.set(null);
    this.rows.set([]);
    this.overwrite.set(false);
  }
}
