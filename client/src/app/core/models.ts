// Mirror of the API read/write DTOs (ClassificaLega.Api.Dtos).

export interface Season {
  id: number;
  name: string;
  totalStages: number;
  countingStages: number;
  isActive: boolean;
}

export interface StandingRow {
  position: number;
  playerId: number;
  displayName: string;
  bestN: number;
  totalPoints: number;
}

export interface Stage {
  id: number;
  number: number;
  name: string | null;
  date: string | null;
  eventLinkId: string | null;
  resultCount: number;
}

export interface StageResult {
  id: number;
  playerId: number;
  displayName: string;
  matchPoints: number;
  bonusRisultato: number;
  bonusPartecipazione: number;
  totalPoints: number;
}

export interface ProgressionPoint {
  stageNumber: number;
  stageTotal: number | null;
  cumulative: number;
}

export interface Progression {
  playerId: number;
  displayName: string;
  points: ProgressionPoint[];
}

export interface MatrixCell {
  stageNumber: number;
  totalPoints: number | null;
}

export interface MatrixRow {
  position: number;
  playerId: number;
  displayName: string;
  cells: MatrixCell[];
  bestN: number;
  totalPoints: number;
}

export interface Matrix {
  totalStages: number;
  countingStages: number;
  stageNumbers: number[];
  rows: MatrixRow[];
}

// --- write payloads ---

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
}

export interface UpdateSeasonRequest {
  totalStages: number;
  countingStages: number;
}

export interface UpsertResultRequest {
  stageNumber: number;
  playerId: number | null;
  newPlayerName: string | null;
  matchPoints: number;
}

export interface UpsertStageRequest {
  number: number;
  name?: string | null;
  date?: string | null;
  eventLinkId?: string | null;
}

// --- import PDF ---

export interface ImportPreviewRow {
  position: number;
  name: string;
  matchPoints: number;
  matchedPlayerId: number | null;
  matchedPlayerName: string | null;
  isNew: boolean;
}

export interface ImportPreviewResponse {
  stageNumber: number | null;
  stageName: string | null;
  eventDate: string | null;
  eventLinkId: string | null;
  stageExists: boolean;
  existingResultCount: number;
  rows: ImportPreviewRow[];
}

export interface ImportCommitRow {
  name: string;
  matchPoints: number;
  playerId: number | null;
}

export interface ImportCommitRequest {
  stageNumber: number;
  stageName?: string | null;
  eventDate?: string | null;
  eventLinkId?: string | null;
  overwrite: boolean;
  rows: ImportCommitRow[];
}

export interface ImportCommitResponse {
  stageNumber: number;
  imported: number;
  replaced: number;
  playersCreated: number;
}
