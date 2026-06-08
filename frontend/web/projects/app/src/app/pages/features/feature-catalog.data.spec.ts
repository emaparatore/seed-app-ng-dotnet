import { describe, expect, it } from 'vitest';
import { findSeedFeatureBySlug, seedFeatures } from './feature-catalog.data';

describe('feature catalog data', () => {
  it('keeps unique slugs for every feature page', () => {
    const slugs = seedFeatures.map((feature) => feature.slug);
    expect(new Set(slugs).size).toBe(slugs.length);
  });

  it('finds known features by slug', () => {
    expect(findSeedFeatureBySlug('authentication')?.title).toContain('Authentication');
  });

  it('returns undefined for missing slugs', () => {
    expect(findSeedFeatureBySlug('missing-feature')).toBeUndefined();
  });
});
