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

  token = computed<string | null>(() => {
    this.version();
    return this.readValidToken(this.scope());
  });

  isAdmin = computed(() => this.token() !== null);
  isSuperAdmin = computed(() => decodeRole(this.token()) === 'SuperAdmin');

  login(req: LoginRequest): Observable<unknown> {
    const scope = this.scope();
    return this.api.login(req).pipe(
      tap((res) => {
        localStorage.setItem(tokenKey(scope), res.token);
        localStorage.setItem(expiryKey(scope), res.expiresAt);
        this.version.update((v) => v + 1);
      }),
    );
  }

  logout(): void {
    const scope = this.scope();
    localStorage.removeItem(tokenKey(scope));
    localStorage.removeItem(expiryKey(scope));
    this.version.update((v) => v + 1);
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
