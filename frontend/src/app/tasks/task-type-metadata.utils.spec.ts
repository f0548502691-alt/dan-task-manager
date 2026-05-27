import assert from 'node:assert/strict';
import test from 'node:test';

import {
  buildStatusOptions,
  buildTaskTypeMetadataState,
  getSuggestedStatus,
  normalizeTaskTypes
} from './task-type-metadata.utils';
import { DEFAULT_TASK_FINAL_STATUS_BY_TYPE, TASK_STATUS, TaskTypeSchemaDto } from './task.interfaces';

function taskType(taskType: string, finalStatus: number | null, isActive = true): TaskTypeSchemaDto {
  return {
    taskType,
    displayName: `${taskType} display`,
    finalStatus,
    isActive,
    version: 1,
    fields: []
  };
}

test('normalizes task types by removing inactive and blank entries before sorting', () => {
  const normalized = normalizeTaskTypes([
    taskType('Zeta', TASK_STATUS.DONE),
    taskType('Inactive', TASK_STATUS.RELEASED, false),
    taskType('', TASK_STATUS.READY_FOR_REVIEW),
    taskType('Alpha', TASK_STATUS.READY_FOR_REVIEW)
  ]);

  assert.deepEqual(
    normalized.map((metadata) => metadata.taskType),
    ['Alpha', 'Zeta']
  );
});

test('builds metadata state with backend final statuses while preserving defaults', () => {
  const state = buildTaskTypeMetadataState([
    taskType('Analysis', TASK_STATUS.READY_FOR_REVIEW),
    taskType('Support', null)
  ]);

  assert.deepEqual(state.taskTypeOptions, ['Analysis', 'Support']);
  assert.equal(state.finalStatusByType['Analysis'], TASK_STATUS.READY_FOR_REVIEW);
  assert.equal(state.finalStatusByType['Support'], undefined);
  assert.equal(state.finalStatusByType['Development'], DEFAULT_TASK_FINAL_STATUS_BY_TYPE['Development']);
});

test('uses metadata final statuses for options and suggested next status', () => {
  const state = buildTaskTypeMetadataState([taskType('Analysis', TASK_STATUS.READY_FOR_REVIEW)]);
  const task = {
    taskType: 'Analysis',
    currentStatus: TASK_STATUS.IN_PROGRESS
  };

  assert.deepEqual(buildStatusOptions(task, state.finalStatusByType), [
    { value: TASK_STATUS.IN_PROGRESS, label: 'In Progress' },
    { value: TASK_STATUS.READY_FOR_REVIEW, label: 'Ready for Review' }
  ]);
  assert.equal(getSuggestedStatus(task, state.finalStatusByType), TASK_STATUS.READY_FOR_REVIEW);
});

test('falls back to the current status for unknown task types', () => {
  const task = {
    taskType: 'Unknown',
    currentStatus: TASK_STATUS.DONE
  };

  assert.deepEqual(buildStatusOptions(task, {}), [
    { value: TASK_STATUS.IN_PROGRESS, label: 'In Progress' },
    { value: TASK_STATUS.READY_FOR_REVIEW, label: 'Ready for Review' },
    { value: TASK_STATUS.DONE, label: 'Done' }
  ]);
  assert.equal(getSuggestedStatus(task, {}), TASK_STATUS.DONE);
});
