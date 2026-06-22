import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import {
  ImportCommitResponse, ImportPreviewResponse, PlayerSelection, Season, Stage,
} from '../../core/models';
import { PlayerPickerComponent } from '../../core/player-picker.component';

interface RowState {
  parsedName: string;          // nome letto dal PDF (colonna "Nome (PDF)")
  matchPoints: number;
  // Giocatore di destinazione: esistente, nuovo (nome editabile) o nessuno (riga ignorata).
  selection: PlayerSelection;
}

@Component({
  selector: 'app-importazione',
  imports: [FormsModule, PlayerPickerComponent],
  templateUrl: './importazione.component.html',
  styleUrl: './importazione.component.scss',
})
export class ImportazioneComponent {
  private api = inject(ApiService);

  season = signal<Season | null>(null);
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
          parsedName: r.name,
          matchPoints: r.matchPoints,
          selection: r.matchedPlayerId !== null
            ? { kind: 'existing', id: r.matchedPlayerId, displayName: r.matchedPlayerName ?? r.name }
            : { kind: 'new', name: r.name },
        } as RowState)));
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.error ?? 'Errore nella lettura del PDF.');
        this.busy.set(false);
      },
    });
    input.value = ''; // allow re-selecting the same file
  }

  setRowSelection(index: number, selection: PlayerSelection): void {
    this.rows.update((rows) =>
      rows.map((r, i) => (i === index ? { ...r, selection } : r)));
  }

  setRowPoints(index: number, value: number): void {
    this.rows.update((rows) =>
      rows.map((r, i) => (i === index ? { ...r, matchPoints: value } : r)));
  }

  matchedCount(): number {
    return this.rows().filter((r) => r.selection?.kind === 'existing').length;
  }
  newCount(): number {
    return this.rows().filter((r) => r.selection?.kind === 'new').length;
  }
  skippedCount(): number {
    return this.rows().filter((r) => r.selection === null).length;
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
      // Le righe senza selezione (selection null) sono ignorate.
      rows: this.rows()
        .filter((r) => r.selection !== null)
        .map((r) => ({
          name: r.selection!.kind === 'new' ? r.selection!.name : r.selection!.displayName,
          matchPoints: r.matchPoints,
          playerId: r.selection!.kind === 'existing' ? r.selection!.id : null,
        })),
    }).subscribe({
      next: (res) => {
        this.done.set(res);
        this.busy.set(false);
        this.preview.set(null);
        this.rows.set([]);
        // refresh stages for next import
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
