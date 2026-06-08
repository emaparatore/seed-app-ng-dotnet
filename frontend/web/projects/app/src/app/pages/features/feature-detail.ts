import { Component, computed, inject } from '@angular/core';
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
export class FeatureDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly slug = toSignal(this.route.paramMap.pipe(map((params) => params.get('slug'))), { initialValue: null });

  protected readonly feature = computed(() => findSeedFeatureBySlug(this.slug()));
  protected readonly relatedFeatures = seedFeatures;
}
