// Mirror della logica pura di ClassificaLega.Domain.Services.ScoringService, parametrizzata sulla
// ScoringRule della stagione. Solo presentazione (anteprima live in inserimento): la verità è il server.

import { ScoringRule } from './models';

export function matchPoints(wins: number, draws: number, losses: number, rule: ScoringRule): number {
  return wins * rule.pointsPerWin + draws * rule.pointsPerDraw + losses * rule.pointsPerLoss;
}

// Soglia più alta <= matchPoints (0 se nessuna).
export function scoreBonus(mp: number, rule: ScoringRule): number {
  return rule.scoreBonuses
    .filter((b) => b.fromMatchPoints <= mp)
    .reduce((best, b) => (b.fromMatchPoints > best.fromMatchPoints ? b : best),
      { fromMatchPoints: -1, points: 0 }).points;
}

// 0 se position assente o non mappata.
export function positionBonus(position: number | null, rule: ScoringRule): number {
  if (position == null) return 0;
  return rule.positionBonuses.find((b) => b.position === position)?.points ?? 0;
}

// Fascia più alta con fromTournament <= indice progressivo 1-based (0 se nessuna).
export function participationPoints(progressiveIndex: number, rule: ScoringRule): number {
  return rule.participationTiers
    .filter((t) => t.fromTournament <= progressiveIndex)
    .reduce((best, t) => (t.fromTournament > best.fromTournament ? t : best),
      { fromTournament: -1, pointsPerParticipation: 0 }).pointsPerParticipation;
}
