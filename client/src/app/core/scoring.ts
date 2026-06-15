// Mirror of ClassificaLega.Domain.Services.ScoringService bonus logic (Appendice A.1/A.2/A.3),
// used for the live preview in the insertion form.

export function bonusRisultato(matchPoints: number): number {
  switch (matchPoints) {
    case 12: return 8;
    case 10: return 6;
    case 9: return 4;
    case 8: return 3;
    case 7: return 2;
    case 6: return 1;
    default: return 0;
  }
}

// previousParticipations < 5 -> 1, else 2.
export function bonusPartecipazione(previousParticipations: number): number {
  return previousParticipations < 5 ? 1 : 2;
}

export function totalPoints(matchPoints: number, prevParticipations: number): number {
  return matchPoints + bonusRisultato(matchPoints) + bonusPartecipazione(prevParticipations);
}
