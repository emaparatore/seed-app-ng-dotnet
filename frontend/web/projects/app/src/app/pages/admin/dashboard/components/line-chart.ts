import { Component, computed, input } from '@angular/core';
import { DailyRegistration } from '../models/dashboard.models';

@Component({
  selector: 'app-line-chart',
  template: `
    @if (data().length > 0) {
      <svg [attr.viewBox]="'0 0 ' + width + ' ' + height" class="line-chart">
        <!-- Grid lines -->
        @for (y of yTicks(); track y.value) {
          <line [attr.x1]="padding.left" [attr.y1]="y.pos" [attr.x2]="width - padding.right" [attr.y2]="y.pos" class="grid-line" />
          <text [attr.x]="padding.left - 8" [attr.y]="y.pos + 4" class="axis-label" text-anchor="end">{{ y.value }}</text>
        }

        <!-- X-axis labels -->
        @for (label of xLabels(); track label.text) {
          <text [attr.x]="label.x" [attr.y]="height - 4" class="axis-label" text-anchor="middle">{{ label.text }}</text>
        }

        <!-- Area fill -->
        <path [attr.d]="areaPath()" class="chart-area" />

        <!-- Line -->
        <polyline [attr.points]="polylinePoints()" class="chart-line" />

        <!-- Data points -->
        @for (point of points(); track point.x) {
          <circle [attr.cx]="point.x" [attr.cy]="point.y" r="3" class="chart-point">
            <title>{{ point.date }}: {{ point.count }}</title>
          </circle>
        }
      </svg>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .line-chart {
      width: 100%;
      height: auto;
    }

    .grid-line {
      stroke: var(--mat-sys-outline-variant, rgba(0, 0, 0, 0.12));
      stroke-width: 1;
    }

    .axis-label {
      fill: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
      font-size: 10px;
    }

    .chart-area {
      fill: var(--mat-sys-primary, #6750a4);
      opacity: 0.1;
    }

    .chart-line {
      fill: none;
      stroke: var(--mat-sys-primary, #6750a4);
      stroke-width: 2;
      stroke-linejoin: round;
      stroke-linecap: round;
    }

    .chart-point {
      fill: var(--mat-sys-primary, #6750a4);
      cursor: pointer;

      &:hover {
        r: 5;
        fill: var(--mat-sys-primary, #6750a4);
      }
    }
  `,
})
export class LineChart {
  readonly data = input.required<DailyRegistration[]>();

  protected readonly width = 600;
  protected readonly height = 200;
  protected readonly padding = { top: 16, right: 16, bottom: 24, left: 40 };

  protected readonly maxCount = computed(() => {
    const max = Math.max(...this.data().map((d) => d.count), 1);
    return Math.ceil(max * 1.1);
  });

  protected readonly points = computed(() => {
    const d = this.data();
    if (d.length === 0) return [];
    const chartW = this.width - this.padding.left - this.padding.right;
    const chartH = this.height - this.padding.top - this.padding.bottom;
    const max = this.maxCount();
    return d.map((item, i) => ({
      x: this.padding.left + (d.length > 1 ? (i / (d.length - 1)) * chartW : chartW / 2),
      y: this.padding.top + chartH - (item.count / max) * chartH,
      date: item.date,
      count: item.count,
    }));
  });

  protected readonly polylinePoints = computed(() => this.points().map((p) => `${p.x},${p.y}`).join(' '));

  protected readonly areaPath = computed(() => {
    const pts = this.points();
    if (pts.length === 0) return '';
    const bottom = this.height - this.padding.bottom;
    return `M ${pts[0].x},${bottom} ` + pts.map((p) => `L ${p.x},${p.y}`).join(' ') + ` L ${pts[pts.length - 1].x},${bottom} Z`;
  });

  protected readonly yTicks = computed(() => {
    const max = this.maxCount();
    const chartH = this.height - this.padding.top - this.padding.bottom;
    const tickCount = 4;
    return Array.from({ length: tickCount + 1 }, (_, i) => {
      const value = Math.round((max / tickCount) * i);
      const pos = this.padding.top + chartH - (value / max) * chartH;
      return { value, pos };
    });
  });

  protected readonly xLabels = computed(() => {
    const d = this.data();
    if (d.length === 0) return [];
    const chartW = this.width - this.padding.left - this.padding.right;
    const step = Math.max(1, Math.floor(d.length / 6));
    return d
      .filter((_, i) => i % step === 0 || i === d.length - 1)
      .map((item, _, arr) => {
        const idx = d.indexOf(item);
        return {
          x: this.padding.left + (d.length > 1 ? (idx / (d.length - 1)) * chartW : chartW / 2),
          text: new Date(item.date).toLocaleDateString('it-IT', { day: '2-digit', month: '2-digit' }),
        };
      });
  });
}
