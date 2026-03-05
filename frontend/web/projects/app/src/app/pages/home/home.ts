import { isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, PLATFORM_ID, inject } from '@angular/core';

@Component({
  selector: 'app-home',
  imports: [],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home implements OnDestroy {
  private readonly platformId = inject(PLATFORM_ID);
  private animationFrameId: number | null = null;

  ngOnDestroy(): void {
    if (this.animationFrameId !== null) {
      cancelAnimationFrame(this.animationFrameId);
    }
  }

  scrollToSection(event: MouseEvent, sectionId: string): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const section = document.getElementById(sectionId);
    if (!section) {
      return;
    }

    event.preventDefault();

    const navElement = document.querySelector('.top-nav') as HTMLElement | null;
    const navHeight = navElement?.offsetHeight ?? 0;
    const extraOffset = 12;

    const startY = window.scrollY;
    const targetY = section.getBoundingClientRect().top + startY - navHeight - extraOffset;

    this.animateWindowScroll(startY, targetY, 850);
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
