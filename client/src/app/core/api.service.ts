import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateLeagueAdminRequest, CreateLeagueRequest, CreateSeasonRequest, ImportCommitRequest,
  ImportCommitResponse, ImportPreviewResponse, League, LeagueAdmin, LoginRequest, LoginResponse,
  Matrix, PlayerPage, Progression, Season, Stage, StageResult, StandingRow, UpdateLeagueAdminRequest,
  UpdateLeagueRequest, UpdateScoringRuleRequest, UpdateSeasonRequest, UpsertResultRequest, UpsertStageRequest,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = 'api';   // relativa al <base href> → funziona sotto sottocartella

  // --- auth ---
  login(req: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.base}/auth/login`, req);
  }

  // --- leghe (elenco pubblico per picker/selettore) ---
  getLeagues(): Observable<League[]> {
    return this.http.get<League[]>(`${this.base}/leagues`);
  }

  // --- gestione leghe (super-admin) ---
  getAllLeagues(): Observable<League[]> {
    return this.http.get<League[]>(`${this.base}/leagues/all`);
  }
  createLeague(req: CreateLeagueRequest): Observable<League> {
    return this.http.post<League>(`${this.base}/leagues`, req);
  }
  updateLeague(id: number, req: UpdateLeagueRequest): Observable<League> {
    return this.http.put<League>(`${this.base}/leagues/${id}`, req);
  }
  getLeagueAdmins(id: number): Observable<LeagueAdmin[]> {
    return this.http.get<LeagueAdmin[]>(`${this.base}/leagues/${id}/admins`);
  }
  createLeagueAdmin(id: number, req: CreateLeagueAdminRequest): Observable<LeagueAdmin> {
    return this.http.post<LeagueAdmin>(`${this.base}/leagues/${id}/admins`, req);
  }
  updateLeagueAdmin(id: number, userId: number, req: UpdateLeagueAdminRequest): Observable<LeagueAdmin> {
    return this.http.put<LeagueAdmin>(`${this.base}/leagues/${id}/admins/${userId}`, req);
  }
  deleteLeagueAdmin(id: number, userId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/leagues/${id}/admins/${userId}`);
  }

  // --- logo lega ---
  // URL pubblico del logo (slug nel path). `v` per cache-busting dopo un upload.
  logoUrl(slug: string, v?: string | number): string {
    const q = v != null ? `?v=${v}` : '';
    return `${this.base}/leagues/${slug}/logo${q}`;
  }
  // Carica/sostituisce il logo della lega corrente (admin di lega o super-admin).
  uploadLogo(file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<void>(`${this.base}/logo`, form);
  }
  deleteLogo(): Observable<void> {
    return this.http.delete<void>(`${this.base}/logo`);
  }
  // Varianti super-admin: gestiscono il logo di una lega qualsiasi per id.
  uploadLeagueLogo(id: number, file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<void>(`${this.base}/leagues/${id}/logo`, form);
  }
  deleteLeagueLogo(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/leagues/${id}/logo`);
  }

  // --- read (public) ---
  getSeason(): Observable<Season> {
    return this.http.get<Season>(`${this.base}/season`);
  }
  getSeasons(): Observable<Season[]> {
    return this.http.get<Season[]>(`${this.base}/seasons`);
  }
  getStandings(): Observable<StandingRow[]> {
    return this.http.get<StandingRow[]>(`${this.base}/standings`);
  }
  getStages(): Observable<Stage[]> {
    return this.http.get<Stage[]>(`${this.base}/stages`);
  }
  // Picker giocatori: ricerca + paginazione lato server.
  getPlayers(search: string, skip = 0, take = 20): Observable<PlayerPage> {
    let params = new HttpParams().set('skip', skip).set('take', take);
    if (search) params = params.set('search', search);
    return this.http.get<PlayerPage>(`${this.base}/players`, { params });
  }
  getStageResults(number: number): Observable<StageResult[]> {
    return this.http.get<StageResult[]>(`${this.base}/stages/${number}/results`);
  }
  getProgression(playerId: number): Observable<Progression> {
    return this.http.get<Progression>(`${this.base}/players/${playerId}/progression`);
  }
  getMatrix(): Observable<Matrix> {
    return this.http.get<Matrix>(`${this.base}/matrix`);
  }

  // --- write (admin) ---
  updateSeason(req: UpdateSeasonRequest): Observable<Season> {
    return this.http.put<Season>(`${this.base}/season`, req);
  }
  createSeason(req: CreateSeasonRequest): Observable<Season> {
    return this.http.post<Season>(`${this.base}/seasons`, req);
  }
  updateScoringRule(req: UpdateScoringRuleRequest): Observable<Season> {
    return this.http.put<Season>(`${this.base}/season/scoring-rule`, req);
  }
  upsertStage(req: UpsertStageRequest): Observable<Stage> {
    return this.http.post<Stage>(`${this.base}/stages`, req);
  }
  upsertResult(req: UpsertResultRequest): Observable<StageResult> {
    return this.http.post<StageResult>(`${this.base}/results`, req);
  }
  updateResult(id: number, matchPoints: number): Observable<StageResult> {
    return this.http.put<StageResult>(`${this.base}/results/${id}`, { matchPoints });
  }
  deleteResult(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/results/${id}`);
  }

  // --- import PDF (admin) ---
  importPdfPreview(file: File): Observable<ImportPreviewResponse> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ImportPreviewResponse>(`${this.base}/import/pdf`, form);
  }
  importCommit(req: ImportCommitRequest): Observable<ImportCommitResponse> {
    return this.http.post<ImportCommitResponse>(`${this.base}/import/commit`, req);
  }
}
