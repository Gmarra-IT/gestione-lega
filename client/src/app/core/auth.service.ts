import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { LoginRequest } from './models';

const TOKEN_KEY = 'classifica.token';
const EXPIRY_KEY = 'classifica.expiresAt';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private api = inject(ApiService);

  private _token = signal<string | null>(this.readValidToken());
  token = this._token.asReadonly();
  isAdmin = computed(() => this._token() !== null);

  login(req: LoginRequest): Observable<unknown> {
    return this.api.login(req).pipe(
      tap((res) => {
        localStorage.setItem(TOKEN_KEY, res.token);
        localStorage.setItem(EXPIRY_KEY, res.expiresAt);
        this._token.set(res.token);
      }),
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EXPIRY_KEY);
    this._token.set(null);
  }

  private readValidToken(): string | null {
    const token = localStorage.getItem(TOKEN_KEY);
    const expiry = localStorage.getItem(EXPIRY_KEY);
    if (!token || !expiry) return null;
    if (new Date(expiry).getTime() <= Date.now()) {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(EXPIRY_KEY);
      return null;
    }
    return token;
  }
}
