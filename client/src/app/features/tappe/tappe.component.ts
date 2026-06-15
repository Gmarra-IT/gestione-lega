import { AfterViewInit, Component, ElementRef, OnDestroy, effect, inject, signal, viewChild } from '@angular/core';
import { Chart, ChartConfiguration, registerables } from 'chart.js';
import { ApiService } from '../../core/api.service';
import { Matrix, MatrixRow } from '../../core/models';

Chart.register(...registerables);

const PALETTE = [
  '#2563eb', '#dc2626', '#16a34a', '#d97706', '#7c3aed',
  '#0891b2', '#db2777', '#65a30d', '#475569', '#ea580c',
];

@Component({
  selector: 'app-tappe',
  templateUrl: './tappe.component.html',
  styleUrl: './tappe.component.scss',
})
export class TappeComponent implements AfterViewInit, OnDestroy {
  private api = inject(ApiService);
  private canvas = viewChild<ElementRef<HTMLCanvasElement>>('chart');

  matrix = signal<Matrix | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  selected = signal<Set<number>>(new Set());

  private chart: Chart | null = null;
  private viewReady = signal(false);

  constructor() {
    this.api.getMatrix().subscribe({
      next: (m) => {
        this.matrix.set(m);
        // pre-select the top 5 by position
        this.selected.set(new Set(m.rows.slice(0, 5).map((r) => r.playerId)));
        this.loading.set(false);
      },
      error: () => { this.error.set('Errore nel caricamento delle tappe.'); this.loading.set(false); },
    });

    effect(() => {
      const m = this.matrix();
      this.selected();
      if (this.viewReady() && m) this.renderChart(m);
    });
  }

  ngAfterViewInit(): void {
    this.viewReady.set(true);
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  toggle(playerId: number): void {
    const next = new Set(this.selected());
    next.has(playerId) ? next.delete(playerId) : next.add(playerId);
    this.selected.set(next);
  }

  isSelected(playerId: number): boolean {
    return this.selected().has(playerId);
  }

  colorFor(row: MatrixRow): string {
    return PALETTE[(row.position - 1) % PALETTE.length];
  }

  private cumulative(row: MatrixRow): number[] {
    let sum = 0;
    return row.cells.map((c) => { sum += c.totalPoints ?? 0; return sum; });
  }

  private renderChart(m: Matrix): void {
    const el = this.canvas()?.nativeElement;
    if (!el) return;

    const datasets = m.rows
      .filter((r) => this.selected().has(r.playerId))
      .map((r) => {
        const color = this.colorFor(r);
        return {
          label: r.displayName,
          data: this.cumulative(r),
          borderColor: color,
          backgroundColor: color,
          tension: 0.25,
          spanGaps: true,
        };
      });

    const config: ChartConfiguration = {
      type: 'line',
      data: { labels: m.stageNumbers.map((n) => `T${n}`), datasets },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'bottom' } },
        scales: { y: { beginAtZero: true, title: { display: true, text: 'Punti cumulati' } } },
      },
    };

    if (this.chart) {
      this.chart.data = config.data;
      this.chart.update();
    } else {
      this.chart = new Chart(el, config);
    }
  }
}
