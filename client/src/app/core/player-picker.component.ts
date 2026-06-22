import {
  ChangeDetectionStrategy, Component, ElementRef, effect, inject, input, model, signal, viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged, fromEvent, merge, switchMap } from 'rxjs';
import { ApiService } from './api.service';
import { PlayerLite, PlayerSelection } from './models';

/**
 * Picker giocatori server-side: typeahead con ricerca + paginazione ("carica altri").
 * Scala con l'arrivo di nuovi player (non scarica l'intera lista). Emette una `PlayerSelection`:
 * un giocatore esistente oppure un nuovo nome da creare (digitando un nome non in elenco).
 * Riusabile ovunque serva selezionare/aggiungere un giocatore (inserimento manuale, revisione import).
 */
@Component({
  selector: 'app-player-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="picker" [class.disabled]="disabled()">
      <div class="field">
        <input
          #box
          type="text"
          [value]="text()"
          [placeholder]="placeholder()"
          [disabled]="disabled()"
          (input)="onInput(box.value)"
          (focus)="onFocus()"
          (blur)="onBlur()"
          (keydown.escape)="close()"
          (keydown.enter)="onEnter($event)"
        />
        @if (text()) {
          <button type="button" class="clear" title="Pulisci" tabindex="-1"
                  (mousedown)="$event.preventDefault()" (click)="clear()">✕</button>
        }
      </div>

      @if (open()) {
        <ul class="menu" (mousedown)="$event.preventDefault()"
            [style.top.px]="menuTop()" [style.left.px]="menuLeft()" [style.width.px]="menuWidth()">
          @if (canCreate()) {
            <li class="create" (click)="create()">➕ Crea «{{ text().trim() }}»</li>
          }
          @for (p of items(); track p.id) {
            <li [class.sel]="p.id === selectedId()" (click)="pick(p)">{{ p.displayName }}</li>
          } @empty {
            @if (!loading() && !canCreate()) { <li class="muted">Nessun giocatore</li> }
          }
          @if (loading()) { <li class="muted">Caricamento…</li> }
          @if (items().length < total()) {
            <li class="more" (click)="loadMore()">Carica altri ({{ total() - items().length }})</li>
          }
        </ul>
      }
    </div>
  `,
  styles: [`
    .picker { position: relative; }
    .field { position: relative; display: flex; }
    .field input { width: 100%; }
    .clear {
      position: absolute; right: .25rem; top: 50%; transform: translateY(-50%);
      border: 0; background: transparent; cursor: pointer; color: var(--muted, #888);
      font-size: .9rem; line-height: 1; padding: .25rem;
    }
    .menu {
      position: fixed; z-index: 1000; margin: .15rem 0 0;
      max-height: 16rem; overflow-y: auto; list-style: none; padding: .25rem;
      background: var(--card-bg, #fff); border: 1px solid var(--border, #ccc);
      border-radius: .4rem; box-shadow: 0 6px 20px rgba(0,0,0,.18);
    }
    .menu li { padding: .4rem .55rem; border-radius: .3rem; cursor: pointer; }
    .menu li:hover { background: var(--hover-bg, rgba(0,0,0,.06)); }
    .menu li.sel { font-weight: 600; }
    .menu li.muted { color: var(--muted, #888); cursor: default; }
    .menu li.muted:hover { background: transparent; }
    .menu li.create { color: var(--accent, #2563eb); font-weight: 600; }
    .menu li.more { color: var(--accent, #2563eb); text-align: center; }
    .disabled { opacity: .6; pointer-events: none; }
  `],
})
export class PlayerPickerComponent {
  private api = inject(ApiService);

  /** Two-way: selezione corrente (esistente | nuovo | null). */
  selection = model<PlayerSelection>(null);
  /** Consenti la creazione di un nuovo giocatore digitando un nome non in elenco. */
  allowNew = input(true);
  placeholder = input('Cerca o digita un nome…');
  disabled = input(false);

  private box = viewChild<ElementRef<HTMLInputElement>>('box');

  private static readonly TAKE = 20;
  text = signal('');
  selectedId = signal<number | null>(null);
  items = signal<PlayerLite[]>([]);
  total = signal(0);
  loading = signal(false);
  open = signal(false);
  // Posizione del menu (position: fixed) calcolata dal rect dell'input: evita il clipping
  // quando il picker vive dentro un contenitore con overflow (es. tabella import scrollabile).
  menuTop = signal(0);
  menuLeft = signal(0);
  menuWidth = signal(0);

  private search$ = new Subject<string>();
  private blurTimer?: ReturnType<typeof setTimeout>;
  // Ultimo valore che abbiamo emesso noi: distingue i cambi interni da quelli esterni (parent),
  // così l'effect di sync non sovrascrive il testo mentre l'utente scrive.
  private lastEmitted: PlayerSelection = null;

  constructor() {
    // Sync testo ⟵ selection quando cambia dall'esterno (reset form, riga import pre-abbinata).
    effect(() => {
      const sel = this.selection();
      if (sel === this.lastEmitted) return;
      this.lastEmitted = sel;
      this.applyIncoming(sel);
    });

    this.search$.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((term) => {
        this.loading.set(true);
        return this.api.getPlayers(term, 0, PlayerPickerComponent.TAKE);
      }),
      takeUntilDestroyed(),
    ).subscribe((page) => {
      this.items.set(page.items);
      this.total.set(page.total);
      this.loading.set(false);
    });

    // Riposiziona il menu fixed quando la pagina/contenitore scrolla o si ridimensiona.
    merge(fromEvent(window, 'scroll', { capture: true }), fromEvent(window, 'resize'))
      .pipe(takeUntilDestroyed())
      .subscribe(() => { if (this.open()) this.positionMenu(); });
  }

  private positionMenu(): void {
    const el = this.box()?.nativeElement;
    if (!el) return;
    const r = el.getBoundingClientRect();
    this.menuTop.set(r.bottom);
    this.menuLeft.set(r.left);
    this.menuWidth.set(r.width);
  }

  // --- input handlers ---

  onInput(value: string): void {
    this.text.set(value);
    this.selectedId.set(null);
    const name = value.trim();
    this.emit(this.allowNew() && name ? { kind: 'new', name } : null);
    this.open.set(true);
    this.positionMenu();
    this.search$.next(name);
  }

  onFocus(): void {
    clearTimeout(this.blurTimer);
    this.open.set(true);
    this.positionMenu();
    if (this.items().length === 0 && !this.loading()) this.search$.next(this.text().trim());
  }

  onBlur(): void {
    this.blurTimer = setTimeout(() => this.open.set(false), 150);
  }

  onEnter(ev: Event): void {
    ev.preventDefault();
    if (this.canCreate()) { this.create(); return; }
    const only = this.items();
    if (only.length === 1) this.pick(only[0]);
  }

  // --- selection ---

  pick(p: PlayerLite): void {
    this.selectedId.set(p.id);
    this.text.set(p.displayName);
    this.emit({ kind: 'existing', id: p.id, displayName: p.displayName });
    this.close();
  }

  create(): void {
    const name = this.text().trim();
    if (!name) return;
    this.selectedId.set(null);
    this.emit({ kind: 'new', name });
    this.close();
  }

  clear(): void {
    this.text.set('');
    this.selectedId.set(null);
    this.emit(null);
    this.items.set([]);
    this.total.set(0);
    this.box()?.nativeElement.focus();
    this.open.set(true);
    this.positionMenu();
    this.search$.next('');
  }

  loadMore(): void {
    this.loading.set(true);
    this.api.getPlayers(this.text().trim(), this.items().length, PlayerPickerComponent.TAKE)
      .subscribe((page) => {
        this.items.update((cur) => [...cur, ...page.items]);
        this.total.set(page.total);
        this.loading.set(false);
      });
  }

  close(): void {
    this.open.set(false);
  }

  /** "Crea «x»" visibile solo se consentito, c'è testo e nessun match esatto in elenco. */
  canCreate(): boolean {
    const name = this.text().trim();
    if (!this.allowNew() || !name) return false;
    const lower = name.toLowerCase();
    return !this.items().some((p) => p.displayName.toLowerCase() === lower);
  }

  // Imposta la selezione marcandola come "nostra" così l'effect di sync la ignora.
  private emit(sel: PlayerSelection): void {
    this.lastEmitted = sel;
    this.selection.set(sel);
  }

  private applyIncoming(sel: PlayerSelection): void {
    if (!sel) { this.text.set(''); this.selectedId.set(null); return; }
    if (sel.kind === 'existing') { this.text.set(sel.displayName); this.selectedId.set(sel.id); }
    else { this.text.set(sel.name); this.selectedId.set(null); }
  }
}
