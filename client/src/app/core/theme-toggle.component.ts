import { Component, computed, inject } from '@angular/core';
import { ThemeService } from './theme.service';

/** Bottone toggle tema dark/light. Riutilizzabile in tutte le testate. */
@Component({
  selector: 'app-theme-toggle',
  template: `
    <button
      type="button"
      class="theme-toggle"
      [attr.aria-label]="isDark() ? 'Passa al tema chiaro' : 'Passa al tema scuro'"
      [attr.aria-pressed]="isDark()"
      title="Tema chiaro/scuro"
      (click)="theme.toggle()"
    >
      @if (isDark()) {
        <!-- sole -->
        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
          <circle cx="12" cy="12" r="4" />
          <path
            d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"
          />
        </svg>
      } @else {
        <!-- luna -->
        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
          <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z" />
        </svg>
      }
    </button>
  `,
  styles: `
    .theme-toggle {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 36px;
      height: 36px;
      padding: 0;
      border: 1px solid var(--border);
      border-radius: 8px;
      background: var(--surface);
      color: var(--text-muted);
      cursor: pointer;
      transition:
        color 0.15s ease,
        border-color 0.15s ease,
        background 0.15s ease;
    }

    .theme-toggle:hover {
      color: var(--text);
      border-color: var(--accent);
    }

    .theme-toggle svg {
      fill: none;
      stroke: currentColor;
      stroke-width: 2;
      stroke-linecap: round;
      stroke-linejoin: round;
    }
  `,
})
export class ThemeToggleComponent {
  protected theme = inject(ThemeService);
  protected isDark = computed(() => this.theme.theme() === 'dark');
}
