export type MachineStatus = 'Offline' | 'Running' | 'Idle' | 'Faulted';
export type Severity = 'Information' | 'Warning' | 'Critical';

export interface Machine {
  id: string;
  machineCode: string;
  name: string;
  status: MachineStatus;
}

export interface MachineDetails extends Machine {
  description: string | null;
  machineType: string;
  location: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface Reading {
  machineId: string;
  timestampUtc: string;
  temperature: number;
  vibration: number;
  rpm: number;
  torque: number;
  toolWear: number;
}

export interface LiveReading extends Reading {
  machineCode: string | null;
  status: MachineStatus | null;
}

export interface Alarm {
  id: number;
  machineId: string;
  alarmCode: string;
  severity: Severity;
  state: 'ActiveUnacknowledged' | 'ActiveAcknowledged' | 'Resolved';
  createdAtUtc: string;
  acknowledgedAtUtc: string | null;
  resolvedAtUtc: string | null;
}

export interface Page<T> {
  page: number;
  pageSize: number;
  hasMore: boolean;
  items: T[];
}

export interface AlarmFilters {
  machineId?: string;
  severity?: Severity;
  active?: boolean;
}

export interface MonitoringEvents {
  MachineReadingUpdated: LiveReading;
  AlarmOpened: {
    alarmId: number;
    machineId: string;
    alarmCode: string;
    severity: Severity;
    createdAtUtc: string;
    isActive: boolean;
    isAcknowledged: boolean;
  };
  AlarmResolved: { alarmId: number; machineId: string; alarmCode: string; resolvedAtUtc: string };
  AlarmAcknowledged: {
    alarmId: number;
    machineId: string;
    acknowledgedAtUtc: string;
    isAcknowledged: boolean;
  };
}
