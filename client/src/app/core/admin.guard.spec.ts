import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { adminGuard } from './admin.guard';
import { AuthService } from './auth.service';

// Snapshot fittizia con paramMap.get('slug') sull'albero route (parent chain).
function routeWithSlug(slug: string | null): ActivatedRouteSnapshot {
  const paramMap = { get: (k: string) => (k === 'slug' ? slug : null) };
  return { paramMap, parent: null } as unknown as ActivatedRouteSnapshot;
}

describe('adminGuard', () => {
  let authSpy: jasmine.SpyObj<AuthService>;
  let routerSpy: jasmine.SpyObj<Router>;
  const state = { url: '/massarosa/impostazioni' } as RouterStateSnapshot;

  beforeEach(() => {
    authSpy = jasmine.createSpyObj<AuthService>('AuthService', ['isAdminForScope']);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['createUrlTree']);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });
  });

  function run(route: ActivatedRouteSnapshot) {
    return TestBed.runInInjectionContext(() => adminGuard(route, state));
  }

  it('consente l\'accesso con token valido per lo slug della route (refresh diretto)', () => {
    authSpy.isAdminForScope.and.returnValue(true);
    const result = run(routeWithSlug('massarosa'));
    expect(authSpy.isAdminForScope).toHaveBeenCalledWith('massarosa');
    expect(result).toBe(true);
    expect(routerSpy.createUrlTree).not.toHaveBeenCalled();
  });

  it('rimbalza al login dello slug senza token valido', () => {
    authSpy.isAdminForScope.and.returnValue(false);
    const tree = {} as UrlTree;
    routerSpy.createUrlTree.and.returnValue(tree);
    const result = run(routeWithSlug('massarosa'));
    expect(result).toBe(tree);
    expect(routerSpy.createUrlTree).toHaveBeenCalledWith(['/', 'massarosa', 'login'], {
      queryParams: { redirect: state.url },
    });
  });
});
