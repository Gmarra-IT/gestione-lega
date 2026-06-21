import { ActivatedRouteSnapshot, BaseRouteReuseStrategy } from '@angular/router';

// Angular per default riusa i componenti quando cambia solo un parametro di route.
// Qui forziamo la ricreazione del sottoalbero quando cambia lo slug della lega,
// così i componenti rifanno la fetch dei dati della nuova lega (selettore).
export class LeagueReuseStrategy extends BaseRouteReuseStrategy {
  override shouldReuseRoute(future: ActivatedRouteSnapshot, curr: ActivatedRouteSnapshot): boolean {
    if (future.routeConfig !== curr.routeConfig) return false;
    if (future.routeConfig?.path === ':slug') {
      return future.params['slug'] === curr.params['slug'];
    }
    return true;
  }
}
