import { Injectable, computed, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { League, Season } from './models';

// Slug "riservati" che NON sono leghe (route di primo livello dedicate al super-admin).
export const RESERVED_SLUGS = ['gestione'];

@Injectable({ providedIn: 'root' })
export class LeagueContextService {
  private api = inject(ApiService);

  // Slug della lega corrente (null su picker / console super-admin).
  private _slug = signal<string | null>(null);
  slug = this._slug.asReadonly();

  // Elenco leghe attive (cache), per picker e selettore.
  private _leagues = signal<League[]>([]);
  leagues = this._leagues.asReadonly();

  // Bumpato dopo un upload/rimozione logo per forzare il refresh degli <img>.
  private _logoVersion = signal(0);
  logoVersion = this._logoVersion.asReadonly();
  bumpLogoVersion(): void {
    this._logoVersion.update((v) => v + 1);
  }

  // Stagioni della lega corrente (attiva + precedenti), più recenti prima.
  private _seasons = signal<Season[]>([]);
  seasons = this._seasons.asReadonly();

  // Stagione selezionata nel selettore. null = segue la stagione attiva.
  // I componenti dato reagiscono a questo signal per ricaricarsi; l'interceptor
  // lo inoltra come header X-Season-Id alle chiamate API.
  private _selectedSeasonId = signal<number | null>(null);
  selectedSeasonId = this._selectedSeasonId.asReadonly();

  // Lega corrente risolta dall'elenco.
  current = computed(() => this._leagues().find((l) => l.slug === this._slug()) ?? null);

  // Stagione effettivamente in vista: selezionata, altrimenti l'attiva.
  currentSeason = computed(() => {
    const seasons = this._seasons();
    const selected = this._selectedSeasonId();
    return seasons.find((s) => s.id === selected)
      ?? seasons.find((s) => s.isActive)
      ?? null;
  });

  // Branding da mostrare in testata.
  brand = computed(() => {
    const l = this.current();
    return l ? (l.title ?? l.name) : 'Lega Pauper';
  });

  setSlug(slug: string | null): void {
    if (slug === this._slug()) return; // l'effect dello shell rie-esegue: agisci solo sul cambio reale
    this._slug.set(slug);
    // Cambio lega → torna alla stagione attiva e ricarica l'elenco stagioni.
    this._selectedSeasonId.set(null);
    this._seasons.set([]);
    if (slug && !RESERVED_SLUGS.includes(slug)) this.loadSeasons();
  }

  setSelectedSeasonId(id: number | null): void {
    this._selectedSeasonId.set(id);
  }

  loadSeasons(): void {
    this.api.getSeasons().subscribe((s) => this._seasons.set(s));
  }

  loadLeagues(): void {
    this.api.getLeagues().subscribe((ls) => this._leagues.set(ls));
  }

  isKnownSlug(slug: string): boolean {
    return this._leagues().some((l) => l.slug === slug);
  }
}
