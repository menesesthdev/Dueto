import { describe, expect, it } from 'vitest';
import { WALLPAPER_GRADIENTS, gradientFor } from './wallpaper-gradient';

describe('gradientFor', () => {
  it('always gives the same gradient to the same wallpaper', () => {
    expect(gradientFor('aurora-dunes')).toBe(gradientFor('aurora-dunes'));
  });

  it('always returns one of the Tidal gradients', () => {
    for (const id of ['a', 'neon-tide', 'drift-loop', 'x'.repeat(64), '0']) {
      expect(WALLPAPER_GRADIENTS).toContain(gradientFor(id));
    }
  });

  it('spreads different ids over more than one gradient', () => {
    const used = new Set(Array.from({ length: 40 }, (_, index) => gradientFor(`wallpaper-${index}`)));

    expect(used.size).toBeGreaterThan(1);
  });
});
