import { Component, computed, input } from '@angular/core';
import { RoleDistribution } from '../models/dashboard.models';

const COLORS = ['#6750a4', '#625b71', '#7d5260', '#006c4c', '#006590', '#8b5000'];

@Component({
  selector: 'app-donut-chart',
  template: `
    @if (data().length > 0) {
      <div class="donut-container">
        <svg viewBox="0 0 200 200" class="donut-svg">
          @for (segment of segments(); track segment.label) {
            <circle
              cx="100"
              cy="100"
              r="70"
              fill="none"
              [attr.stroke]="segment.color"
              stroke-width="30"
              [attr.stroke-dasharray]="segment.dashArray"
              [attr.stroke-dashoffset]="segment.dashOffset"
              [attr.transform]="'rotate(-90 100 100)'"
            >
              <title>{{ segment.label }}: {{ segment.count }}</title>
            </circle>
          }
          <text x="100" y="96" text-anchor="middle" class="donut-total">{{ total() }}</text>
          <text x="100" y="114" text-anchor="middle" class="donut-total-label">totale</text>
        </svg>
        <div class="donut-legend">
          @for (segment of segments(); track segment.label) {
            <div class="legend-item">
              <span class="legend-dot" [style.background]="segment.color"></span>
              <span class="legend-label">{{ segment.label }}</span>
              <span class="legend-count">{{ segment.count }}</span>
            </div>
          }
        </div>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .donut-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
    }

    .donut-svg {
      width: 180px;
      height: 180px;
    }

    .donut-total {
      font-size: 24px;
      font-weight: 500;
      fill: var(--mat-sys-on-surface, #000);
    }

    .donut-total-label {
      font-size: 12px;
      fill: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
    }

    .donut-legend {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 16px;
      justify-content: center;
    }

    .legend-item {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 13px;
      color: var(--mat-sys-on-surface, #000);
    }

    .legend-dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      flex-shrink: 0;
    }

    .legend-count {
      font-weight: 500;
      color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
    }
  `,
})
export class DonutChart {
  readonly data = input.required<RoleDistribution[]>();

  protected readonly total = computed(() => this.data().reduce((sum, d) => sum + d.userCount, 0));

  protected readonly circumference = 2 * Math.PI * 70;

  protected readonly segments = computed(() => {
    const t = this.total();
    if (t === 0) return [];
    let offset = 0;
    return this.data().map((item, i) => {
      const pct = item.userCount / t;
      const dashLength = pct * this.circumference;
      const segment = {
        label: item.roleName,
        count: item.userCount,
        color: COLORS[i % COLORS.length],
        dashArray: `${dashLength} ${this.circumference - dashLength}`,
        dashOffset: `${-offset}`,
      };
      offset += dashLength;
      return segment;
    });
  });
}
