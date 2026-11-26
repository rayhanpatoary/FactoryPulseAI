import { describe, expect, it } from 'vitest';
import { dateRange } from './history';

describe('telemetry history dates', () => {
  it('converts local bounds to UTC and allows open bounds', () => {
    const local = '2026-11-25T10:30';
    expect(dateRange(local, '')).toEqual({ fromUtc: new Date(local).toISOString(), toUtc: undefined });
    expect(dateRange('', '')).toEqual({ fromUtc: undefined, toUtc: undefined });
  });

  it('rejects invalid or reversed limits', () => {
    expect(dateRange('invalid', '')).toBeNull();
    expect(dateRange('2026-11-25T10:30', '2026-11-24T10:30')).toBeNull();
    expect(dateRange('2026-11-25T10:30', '2026-11-25T10:30')).toBeNull();
  });
});
