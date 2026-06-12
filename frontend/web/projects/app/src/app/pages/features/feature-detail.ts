import { isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, PLATFORM_ID, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { findSeedFeatureBySlug, seedFeatures } from './feature-catalog.data';

@Component({
  selector: 'app-feature-detail',
  imports: [RouterLink],
  templateUrl: './feature-detail.html',
  styleUrl: './feature-detail.scss',
})
export class FeatureDetail implements OnDestroy {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute);
  private readonly slug = toSignal(this.route.paramMap.pipe(map((params) => params.get('slug'))), { initialValue: null });
  private animationFrameId: number | null = null;

  protected readonly feature = computed(() => findSeedFeatureBySlug(this.slug()));
  protected readonly relatedFeatures = seedFeatures;

  ngOnDestroy(): void {
    if (this.animationFrameId !== null) {
      cancelAnimationFrame(this.animationFrameId);
    }
  }

  scrollToSection(sectionId: string): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const section = document.getElementById(sectionId);
    if (!section) {
      return;
    }

    const navElement = document.querySelector('.top-nav') as HTMLElement | null;
    const navHeight = navElement?.offsetHeight ?? 0;
    const extraOffset = 12;

    const startY = window.scrollY;
    const targetY = section.getBoundingClientRect().top + startY - navHeight - extraOffset;

    this.animateWindowScroll(startY, targetY, 650);
  }

  private animateWindowScroll(startY: number, targetY: number, durationMs: number): void {
    if (this.animationFrameId !== null) {
      cancelAnimationFrame(this.animationFrameId);
    }

    const clampedTargetY = Math.max(targetY, 0);
    const distance = clampedTargetY - startY;
    const startTime = performance.now();

    const tick = (currentTime: number): void => {
      const elapsed = currentTime - startTime;
      const progress = Math.min(elapsed / durationMs, 1);
      const easedProgress = this.easeInOutCubic(progress);
      window.scrollTo({ top: startY + distance * easedProgress, left: 0 });

      if (progress < 1) {
        this.animationFrameId = requestAnimationFrame(tick);
        return;
      }

      this.animationFrameId = null;
    };

    this.animationFrameId = requestAnimationFrame(tick);
  }

  private easeInOutCubic(value: number): number {
    if (value < 0.5) {
      return 4 * value * value * value;
    }

    return 1 - Math.pow(-2 * value + 2, 3) / 2;
  }
}
