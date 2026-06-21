import { Injectable, computed, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { League } from './models';

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

  // Lega corrente risolta dall'elenco.
  current = computed(() => this._leagues().find((l) => l.slug === this._slug()) ?? null);

  // Branding da mostrare in testata.
  brand = computed(() => {
    const l = this.current();
    return l ? (l.title ?? l.name) : 'Lega Pauper';
  });

  setSlug(slug: string | null): void {
    this._slug.set(slug);
  }

  loadLeagues(): void {
    this.api.getLeagues().subscribe((ls) => this._leagues.set(ls));
  }

  isKnownSlug(slug: string): boolean {
    return this._leagues().some((l) => l.slug === slug);
  }
}
