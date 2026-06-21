import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { RESERVED_SLUGS } from './league-context.service';

// Slug della lega = primo segmento dell'URL corrente (es. /pisa/classifica → "pisa").
// Derivarlo dall'URL (non da uno stato a parte) evita problemi di timing col ciclo
// di vita dei componenti: l'header riflette sempre la pagina mostrata.
function slugFromUrl(url: string): string | null {
  const seg = url.split('?')[0].split('#')[0].split('/').filter(Boolean)[0];
  if (!seg) return null;
  const slug = decodeURIComponent(seg);
  return RESERVED_SLUGS.includes(slug) ? null : slug;
}

export const leagueInterceptor: HttpInterceptorFn = (req, next) => {
  const slug = slugFromUrl(inject(Router).url);
  if (!slug) return next(req);
  return next(req.clone({ setHeaders: { 'X-League-Slug': slug } }));
};
