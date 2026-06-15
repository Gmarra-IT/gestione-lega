import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  private auth = inject(AuthService);
  private router = inject(Router);

  isAdmin = this.auth.isAdmin;

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/classifica']);
  }
}
