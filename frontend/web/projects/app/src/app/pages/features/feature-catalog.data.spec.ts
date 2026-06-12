import { describe, expect, it } from 'vitest';
import { findSeedFeatureBySlug, seedFeatures } from './feature-catalog.data';

describe('feature catalog data', () => {
  it('keeps unique slugs for every feature page', () => {
    const slugs = seedFeatures.map((feature) => feature.slug);
    expect(new Set(slugs).size).toBe(slugs.length);
  });

  it('keeps every feature page documented with overview and sections', () => {
    for (const feature of seedFeatures) {
      expect(feature.overview.length).toBeGreaterThan(0);
      expect(feature.sections.length).toBeGreaterThan(0);
      expect(feature.docs.length).toBeGreaterThan(0);
      expect(feature.codeAreas.length).toBeGreaterThan(0);
    }
  });

  it('keeps section ids unique within each feature page', () => {
    for (const feature of seedFeatures) {
      const sectionIds = feature.sections.map((section) => section.id);
      expect(new Set(sectionIds).size).toBe(sectionIds.length);
    }
  });

  it('keeps every documentation section renderable', () => {
    for (const feature of seedFeatures) {
      for (const section of feature.sections) {
        expect(section.id).toBeTruthy();
        expect(section.title).toBeTruthy();
        expect(Boolean(section.paragraphs?.length || section.items?.length || section.table || section.code || section.callout)).toBe(true);
      }
    }
  });

  it('finds known features by slug', () => {
    expect(findSeedFeatureBySlug('authentication')?.title).toContain('Authentication');
  });

  it('returns undefined for missing slugs', () => {
    expect(findSeedFeatureBySlug('missing-feature')).toBeUndefined();
  });
});
