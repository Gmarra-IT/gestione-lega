import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { LeagueContextService } from './league-context.service';
import { LoginRequest } from './models';

// Token salvato per "scope": slug della lega, oppure '__super' per la console super-admin.
const SUPER_SCOPE = '__super';
const tokenKey = (scope: string) => `classifica.token.${scope}`;
const expiryKey = (scope: string) => `classifica.expiresAt.${scope}`;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private api = inject(ApiService);
  private ctx = inject(LeagueContextService);

  // Bump a ogni login/logout per far ricalcolare i computed.
  private version = signal(0);

  private scope = computed(() => this.ctx.slug() ?? SUPER_SCOPE);

  // Token effettivo per lo scope corrente: token della lega, oppure fallback al
  // token super-admin (globale, vale su qualsiasi lega).
  token = computed<string | null>(() => {
    this.version();
    return this.readValidToken(this.scope()) ?? this.readValidToken(SUPER_SCOPE);
  });

  isAdmin = computed(() => this.token() !== null);
  // Super-admin e' globale: si valuta solo dallo scope super, indipendente dalla lega corrente.
  isSuperAdmin = computed(() => {
    this.version();
    return decodeRole(this.readValidToken(SUPER_SCOPE)) === 'SuperAdmin';
  });

  // Esiste un token valido per lo scope di questo slug? (slug null => scope super-admin)
  // Esplicito: usato dai guard su caricamento diretto/refresh, quando LeagueContext non e' ancora popolato.
  // Il token super-admin abilita qualsiasi lega.
  isAdminForScope(slug: string | null): boolean {
    this.version();
    return this.readValidToken(slug ?? SUPER_SCOPE) !== null || this.readValidToken(SUPER_SCOPE) !== null;
  }

  login(req: LoginRequest): Observable<unknown> {
    const scope = this.scope();
    return this.api.login(req).pipe(
      tap((res) => {
        this.store(scope, res.token, res.expiresAt);
        // Login super-admin (anche fatto dalla pagina di una lega): replica sotto lo
        // scope super cosi' vale ovunque.
        if (decodeRole(res.token) === 'SuperAdmin') {
          this.store(SUPER_SCOPE, res.token, res.expiresAt);
        }
        this.version.update((v) => v + 1);
      }),
    );
  }

  logout(): void {
    const scope = this.scope();
    this.clear(scope);
    // Se la sessione attiva e' super-admin, esci anche dallo scope globale.
    if (decodeRole(this.readValidToken(SUPER_SCOPE)) === 'SuperAdmin') {
      this.clear(SUPER_SCOPE);
    }
    this.version.update((v) => v + 1);
  }

  private store(scope: string, token: string, expiresAt: string): void {
    localStorage.setItem(tokenKey(scope), token);
    localStorage.setItem(expiryKey(scope), expiresAt);
  }

  private clear(scope: string): void {
    localStorage.removeItem(tokenKey(scope));
    localStorage.removeItem(expiryKey(scope));
  }

  private readValidToken(scope: string): string | null {
    const token = localStorage.getItem(tokenKey(scope));
    const expiry = localStorage.getItem(expiryKey(scope));
    if (!token || !expiry) return null;
    if (new Date(expiry).getTime() <= Date.now()) {
      localStorage.removeItem(tokenKey(scope));
      localStorage.removeItem(expiryKey(scope));
      return null;
    }
    return token;
  }
}

function decodeRole(token: string | null): string | null {
  if (!token) return null;
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? payload['role'] ?? null;
  } catch {
    return null;
  }
}
