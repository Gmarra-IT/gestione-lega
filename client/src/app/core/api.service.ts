import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateLeagueAdminRequest, CreateLeagueRequest, ImportCommitRequest, ImportCommitResponse,
  ImportPreviewResponse, League, LeagueAdmin, LoginRequest, LoginResponse, Matrix, Progression,
  Season, Stage, StageResult, StandingRow, UpdateLeagueRequest, UpdateSeasonRequest,
  UpsertResultRequest, UpsertStageRequest,
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

  // --- read (public) ---
  getSeason(): Observable<Season> {
    return this.http.get<Season>(`${this.base}/season`);
  }
  getStandings(): Observable<StandingRow[]> {
    return this.http.get<StandingRow[]>(`${this.base}/standings`);
  }
  getStages(): Observable<Stage[]> {
    return this.http.get<Stage[]>(`${this.base}/stages`);
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
