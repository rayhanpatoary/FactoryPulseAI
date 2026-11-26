import { Machine, Reading } from './models';

export function compareUtc(left: string, right: string) {
  // The API emits UTC with up to seven fractional digits. Date.parse alone drops sub-millisecond precision.
  const canonical = (value: string) =>
    value.slice(0, 19) +
    value
      .slice(19)
      .replace(/^\.|Z$/g, '')
      .padEnd(7, '0');
  return canonical(left).localeCompare(canonical(right));
}

export function mergeReadings(history: readonly Reading[], incoming: readonly Reading[], limit = 120) {
  const merged = [...history];
  for (const sample of incoming) {
    if (!merged.some((item) => compareUtc(item.timestampUtc, sample.timestampUtc) === 0)) {
      merged.push(sample);
    }
  }
  return merged.sort((left, right) => compareUtc(left.timestampUtc, right.timestampUtc)).slice(-limit);
}

export function machineCounts(machines: readonly Machine[]) {
  const running = machines.filter((machine) => machine.status === 'Running').length;
  return { total: machines.length, running, stopped: machines.length - running };
}
