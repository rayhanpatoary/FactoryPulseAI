import { describe, expect, it } from 'vitest';

describe('Angular workspace', () => {
  it('runs with a browser document', () => {
    expect(document.createElement('main').tagName).toBe('MAIN');
  });
});
