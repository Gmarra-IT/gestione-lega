import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  LoginRequest, LoginResponse, Matrix, Progression, Season, Stage, StageResult,
  StandingRow, UpdateSeasonRequest, UpsertResultRequest, UpsertStageRequest,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = '/api';

  // --- auth ---
  login(req: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.base}/auth/login`, req);
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
}
