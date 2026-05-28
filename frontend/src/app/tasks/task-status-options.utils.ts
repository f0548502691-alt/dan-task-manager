import { DEFAULT_STATUS_LABELS, TASK_STATUS } from './task.interfaces';

export interface StatusOption {
  value: number;
  label: string;
}

const TWO_STATE_CLOSED_STATUS = 2;

export function buildStatusOptions(currentStatus: number, finalStatus: number): StatusOption[] {
  if (currentStatus === TASK_STATUS.CLOSED) {
    return [{ value: TASK_STATUS.CLOSED, label: getStatusLabel(TASK_STATUS.CLOSED) }];
  }

  const maxStatus = Math.max(finalStatus, currentStatus);
  const options: StatusOption[] = [];

  for (let status = TASK_STATUS.CREATED; status <= maxStatus; status += 1) {
    options.push({ value: status, label: getDropdownStatusLabel(status, finalStatus) });
  }

  return options;
}

export function getStatusLabel(status: number): string {
  return DEFAULT_STATUS_LABELS[status] ?? `Status ${status}`;
}

function getDropdownStatusLabel(status: number, finalStatus: number): string {
  if (finalStatus === TWO_STATE_CLOSED_STATUS && status === TWO_STATE_CLOSED_STATUS) {
    return DEFAULT_STATUS_LABELS[TASK_STATUS.CLOSED];
  }

  return getStatusLabel(status);
}
