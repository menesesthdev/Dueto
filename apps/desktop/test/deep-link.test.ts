import { vi, describe, it, expect } from 'vitest';

vi.mock('electron', () => ({
  app: {
    getPath: () => '/tmp/dueto-test',
    isPackaged: false,
  },
  safeStorage: {
    isEncryptionAvailable: () => false,
    encryptString: (s: string) => Buffer.from(s),
    decryptString: (b: Buffer) => b.toString(),
  },
}));

import { parseDeepLink } from '../src/main/deep-link.js';

describe('parseDeepLink', () => {
  it.each([
    ['dueto://apply?id=aurora-dunes', 'aurora-dunes'],
    ['dueto://apply?id=neon-tide-123', 'neon-tide-123'],
    ['dueto://apply?id=a', 'a'],
    [`dueto://apply?id=${'a'.repeat(64)}`, 'a'.repeat(64)],
  ])('accepts %s', (url, expected) => {
    expect(parseDeepLink(url)).toBe(expected);
  });

  it.each([
    ['wrong protocol', 'https://apply?id=aurora-dunes'],
    ['wrong hostname', 'dueto://set?id=aurora-dunes'],
    ['missing id', 'dueto://apply'],
    ['empty id', 'dueto://apply?id='],
    ['uppercase', 'dueto://apply?id=Aurora-Dunes'],
    ['underscore', 'dueto://apply?id=aurora_dunes'],
    ['too long', `dueto://apply?id=${'a'.repeat(65)}`],
    ['garbage', 'not a url'],
  ])('rejects %s', (_label, url) => {
    expect(parseDeepLink(url)).toBeNull();
  });
});
