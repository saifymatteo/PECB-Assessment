import { TicketPriority, TicketStatus } from './models';

export const STATUS_LABELS: Record<TicketStatus, string> = {
  New: 'New',
  InProgress: 'In Progress',
  Resolved: 'Resolved',
  Closed: 'Closed',
};

export const PRIORITY_LABELS: Record<TicketPriority, string> = {
  Low: 'Low',
  Normal: 'Normal',
  High: 'High',
  Critical: 'Critical',
};

/** Button label for a transition TO the given status. */
export function transitionLabel(target: TicketStatus, current: TicketStatus): string {
  if (target === 'InProgress') return current === 'Resolved' ? 'Reopen' : 'Start progress';
  if (target === 'Resolved') return 'Mark resolved';
  if (target === 'Closed') return 'Close ticket';
  return STATUS_LABELS[target];
}
