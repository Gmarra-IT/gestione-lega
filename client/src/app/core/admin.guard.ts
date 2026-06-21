import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

function slugOf(route: ActivatedRouteSnapshot): string | null {
  for (let r: ActivatedRouteSnapshot | null = route; r; r = r.parent) {
    const s = r.paramMap.get('slug');
    if (s) return s;
  }
  return null;
}

export const adminGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const slug = slugOf(route);
  if (auth.isAdminForScope(slug)) return true;
  return router.createUrlTree(['/', slug, 'login'], { queryParams: { redirect: state.url } });
};
