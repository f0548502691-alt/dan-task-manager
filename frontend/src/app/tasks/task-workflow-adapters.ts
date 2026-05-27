import { FormGroup } from '@angular/forms';
import { TASK_STATUS, TaskCustomData } from './task.interfaces';

export interface TaskWorkflowAdapter {
  hydrate(form: FormGroup, data: TaskCustomData): void;
  buildPayload(form: FormGroup, status: number): TaskCustomData;
}

const PROCUREMENT_ADAPTER: TaskWorkflowAdapter = {
  hydrate(form, data) {
    const prices = Array.isArray(data['prices']) ? data['prices'] : [];
    form.patchValue(
      {
        priceA: typeof prices[0] === 'string' ? prices[0] : '',
        priceB: typeof prices[1] === 'string' ? prices[1] : '',
        receipt: typeof data['receipt'] === 'string' ? data['receipt'] : ''
      },
      { emitEvent: false }
    );
  },
  buildPayload(form, status) {
    if (status === TASK_STATUS.STATUS_2) {
      return {
        prices: [form.controls['priceA'].value, form.controls['priceB'].value]
      };
    }

    if (status === TASK_STATUS.STATUS_3) {
      return {
        receipt: form.controls['receipt'].value
      };
    }

    return {};
  }
};

const DEVELOPMENT_ADAPTER: TaskWorkflowAdapter = {
  hydrate(form, data) {
    form.patchValue(
      {
        specification: typeof data['specification'] === 'string' ? data['specification'] : '',
        branchName: typeof data['branchName'] === 'string' ? data['branchName'] : '',
        versionNumber:
          typeof data['versionNumber'] === 'string' || typeof data['versionNumber'] === 'number'
            ? String(data['versionNumber'])
            : ''
      },
      { emitEvent: false }
    );
  },
  buildPayload(form, status) {
    if (status === TASK_STATUS.STATUS_2) {
      return {
        specification: form.controls['specification'].value
      };
    }

    if (status === TASK_STATUS.STATUS_3) {
      return {
        branchName: form.controls['branchName'].value
      };
    }

    if (status === TASK_STATUS.STATUS_4) {
      return {
        versionNumber: form.controls['versionNumber'].value
      };
    }

    return {};
  }
};

const TASK_WORKFLOW_ADAPTERS: Readonly<Record<string, TaskWorkflowAdapter>> = {
  Procurement: PROCUREMENT_ADAPTER,
  Development: DEVELOPMENT_ADAPTER
};

export function getTaskWorkflowAdapter(taskType: string): TaskWorkflowAdapter | undefined {
  return TASK_WORKFLOW_ADAPTERS[taskType];
}
