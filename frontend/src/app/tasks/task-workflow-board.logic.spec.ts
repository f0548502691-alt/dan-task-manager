import test from 'node:test';
import assert from 'node:assert/strict';
import { TASK_STATUS } from './task.interfaces';
import type { BaseTaskDto } from './task.interfaces';
import {
  EMPTY_WORKFLOW_FIELD_VALUES,
  buildWorkflowPayload,
  canCloseTaskStatus,
  getSuggestedTaskStatus,
  getTaskStatusOptions,
  hydrateWorkflowFields,
  safeParseTaskCustomData
} from './task-workflow-board.logic';

test('status options for a closed task expose only the closed state', () => {
  const task = createTask({ currentStatus: TASK_STATUS.CLOSED });

  assert.deepEqual(getTaskStatusOptions(task), [{ value: TASK_STATUS.CLOSED, label: 'Closed' }]);
  assert.equal(canCloseTaskStatus(task.currentStatus), false);
});

test('status options include reverse states and clamp suggested status to the task type final state', () => {
  const developmentTask = createTask({
    taskType: 'Development',
    currentStatus: TASK_STATUS.DONE
  });
  const customTask = createTask({
    taskType: 'Custom',
    currentStatus: 6
  });

  assert.deepEqual(
    getTaskStatusOptions(developmentTask).map((option) => option.value),
    [
      TASK_STATUS.BACKLOG,
      TASK_STATUS.IN_PROGRESS,
      TASK_STATUS.READY_FOR_REVIEW,
      TASK_STATUS.DONE,
      TASK_STATUS.RELEASED
    ]
  );
  assert.equal(getSuggestedTaskStatus(developmentTask), TASK_STATUS.RELEASED);
  assert.deepEqual(
    getTaskStatusOptions(customTask).map((option) => option.label),
    ['Backlog', 'In Progress', 'Ready for Review', 'Done', 'Released', 'Status 5', 'Status 6']
  );
  assert.equal(getSuggestedTaskStatus(customTask), 6);
});

test('workflow field hydration tolerates malformed or unexpected custom data', () => {
  assert.deepEqual(
    hydrateWorkflowFields(
      createTask({
        taskType: 'Procurement',
        customDataJson: JSON.stringify({ prices: ['100'], receipt: 'REC-001' })
      })
    ),
    {
      ...EMPTY_WORKFLOW_FIELD_VALUES,
      priceA: '100',
      receipt: 'REC-001'
    }
  );

  assert.deepEqual(
    hydrateWorkflowFields(
      createTask({
        taskType: 'Development',
        customDataJson: JSON.stringify({
          specification: 'Build checkout',
          branchName: 'feature/cart',
          versionNumber: 42
        })
      })
    ),
    {
      ...EMPTY_WORKFLOW_FIELD_VALUES,
      specification: 'Build checkout',
      branchName: 'feature/cart',
      versionNumber: '42'
    }
  );

  assert.deepEqual(hydrateWorkflowFields(createTask({ taskType: 'Procurement', customDataJson: '{' })), {
    ...EMPTY_WORKFLOW_FIELD_VALUES
  });
});

test('known task types build workflow payloads for backend status validation', () => {
  assert.deepEqual(
    buildWorkflowPayload('Procurement', TASK_STATUS.READY_FOR_REVIEW, {
      ...EMPTY_WORKFLOW_FIELD_VALUES,
      priceA: '5000',
      priceB: '4800'
    }),
    {
      payload: { prices: ['5000', '4800'] },
      invalidFallbackJson: false
    }
  );

  assert.deepEqual(
    buildWorkflowPayload('Procurement', TASK_STATUS.DONE, {
      ...EMPTY_WORKFLOW_FIELD_VALUES,
      receipt: 'REC-2026-001'
    }),
    {
      payload: { receipt: 'REC-2026-001' },
      invalidFallbackJson: false
    }
  );

  assert.deepEqual(
    buildWorkflowPayload('Development', TASK_STATUS.RELEASED, {
      ...EMPTY_WORKFLOW_FIELD_VALUES,
      versionNumber: '2026.05'
    }),
    {
      payload: { versionNumber: '2026.05' },
      invalidFallbackJson: false
    }
  );
});

test('unknown task fallback payloads accept only valid JSON objects', () => {
  assert.deepEqual(
    buildWorkflowPayload('Custom', TASK_STATUS.IN_PROGRESS, {
      ...EMPTY_WORKFLOW_FIELD_VALUES,
      fallbackJson: '{"risk":"low"}'
    }),
    {
      payload: { risk: 'low' },
      invalidFallbackJson: false
    }
  );
  assert.deepEqual(
    buildWorkflowPayload('Custom', TASK_STATUS.IN_PROGRESS, {
      ...EMPTY_WORKFLOW_FIELD_VALUES,
      fallbackJson: '[1,2]'
    }),
    {
      payload: {},
      invalidFallbackJson: false
    }
  );
  assert.deepEqual(
    buildWorkflowPayload('Custom', TASK_STATUS.IN_PROGRESS, {
      ...EMPTY_WORKFLOW_FIELD_VALUES,
      fallbackJson: '{"risk":'
    }),
    {
      payload: {},
      invalidFallbackJson: true
    }
  );
  assert.deepEqual(safeParseTaskCustomData('[1,2]'), {});
});

function createTask(overrides: Partial<BaseTaskDto> = {}): BaseTaskDto {
  return {
    id: 1,
    taskType: 'Procurement',
    currentStatus: TASK_STATUS.IN_PROGRESS,
    assignedToUserId: 1,
    assignedToUser: null,
    description: 'Test task',
    customDataJson: '{}',
    createdAt: '2026-05-26T00:00:00.000Z',
    updatedAt: '2026-05-26T00:00:00.000Z',
    ...overrides
  };
}
