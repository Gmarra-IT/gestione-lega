// Avatar fallback condiviso (testata league-shell + picker leghe):
// usato quando una lega non ha logo. Iniziali dal nome + colore dallo slug.

// Iniziali derivate dal nome: fino a 3 parole → 3 lettere, sennò prime 2 lettere.
export function leagueInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  const letters =
    parts.length >= 2 ? parts.slice(0, 3).map((p) => p[0]).join('') : name.slice(0, 2);
  return letters.toUpperCase();
}

// Colore avatar derivato da un seme (slug della lega) → tonalità stabile.
export function leagueAvatarColor(seed: string): string {
  let h = 0;
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) % 360;
  return `hsl(${h} 55% 45%)`;
}
