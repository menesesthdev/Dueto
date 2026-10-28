import { describe, expect, it } from 'vitest';
import { wallpaperSubtitle } from '../catalog/catalog.page';
import { applyTyped } from './otp-input';
import { pairingErrorMessage } from './pairing-flow';
import { formatCountdown } from './pairing.page';

describe('applyTyped', () => {
  it('appends one typed digit', () => {
    expect(applyTyped('12', '3', 6)).toBe('123');
  });

  it('ignores non-digits', () => {
    expect(applyTyped('12', 'a', 6)).toBe('12');
    expect(applyTyped('12', ' ', 6)).toBe('12');
  });

  it('never goes past the code length', () => {
    expect(applyTyped('123456', '7', 6)).toBe('123456');
  });

  it('replaces everything when several digits arrive at once (paste or SMS autofill)', () => {
    expect(applyTyped('12', '654321', 6)).toBe('654321');
  });

  it('keeps only the digits of a pasted code with spaces or a dash', () => {
    expect(applyTyped('', '482-913', 6)).toBe('482913');
    expect(applyTyped('', ' 48 29 13 ', 6)).toBe('482913');
  });

  it('cuts a pasted value that is too long', () => {
    expect(applyTyped('', '1234567890', 6)).toBe('123456');
  });
});

describe('formatCountdown', () => {
  it.each([
    [300, '5:00'],
    [272, '4:32'],
    [61, '1:01'],
    [9, '0:09'],
    [0, '0:00'],
  ])('%s seconds is %s', (seconds, text) => {
    expect(formatCountdown(seconds)).toBe(text);
  });
});

describe('pairingErrorMessage', () => {
  it('explains a wrong or expired code when entering', () => {
    expect(pairingErrorMessage({ status: 400, title: null }, 'enter')).toContain('Código inválido ou expirado');
  });

  it('does not talk about a wrong code when generating', () => {
    expect(pairingErrorMessage({ status: 400, title: null }, 'generate')).not.toContain('Código inválido');
  });

  it.each([
    [409, 'aparelho desse tipo'],
    [429, 'Aguarde um minuto'],
    [null, 'Sem conexão'],
    [500, 'Algo deu errado'],
  ])('explains status %s', (status, fragment) => {
    expect(pairingErrorMessage({ status, title: null }, 'enter')).toContain(fragment);
  });

  it('never shows the technical title from the API', () => {
    expect(pairingErrorMessage({ status: 400, title: 'Invalid or expired pairing code.' }, 'enter')).not.toContain('Invalid');
  });
});

describe('wallpaperSubtitle', () => {
  const base = { id: 'a', title: 'A', desktopUrl: '', desktopSha256: '', mobileUrl: null, mobileSha256: null, thumbnailUrl: '', createdAt: '' };

  it('marks animated wallpapers as PC-only', () => {
    expect(wallpaperSubtitle({ ...base, kind: 'animated', tags: ['calm'] })).toBe('Animado · só PC');
  });

  it('shows up to two tags for static wallpapers', () => {
    expect(wallpaperSubtitle({ ...base, kind: 'static', tags: ['landscape', 'dark', 'extra'] })).toBe('Estático · landscape, dark');
  });

  it('shows only the kind when there are no tags', () => {
    expect(wallpaperSubtitle({ ...base, kind: 'static', tags: [] })).toBe('Estático');
  });
});
