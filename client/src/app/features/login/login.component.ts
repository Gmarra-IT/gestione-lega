import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { LeagueContextService } from '../../core/league-context.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private ctx = inject(LeagueContextService);

  username = signal('');
  password = signal('');
  error = signal<string | null>(null);
  busy = signal(false);

  submit(): void {
    this.error.set(null);
    this.busy.set(true);
    this.auth.login({ username: this.username(), password: this.password() }).subscribe({
      next: () => {
        const redirect = this.route.snapshot.queryParamMap.get('redirect');
        if (redirect) this.router.navigateByUrl(redirect);
        else this.router.navigate(['/', this.ctx.slug(), 'inserimento']);
      },
      error: (err) => {
        this.error.set(err.status === 401 ? 'Credenziali non valide.' : 'Errore di accesso.');
        this.busy.set(false);
      },
    });
  }
}
