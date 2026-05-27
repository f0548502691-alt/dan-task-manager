import assert from 'node:assert/strict';
import test from 'node:test';

import { normalizeChangeStatusResponse, normalizeTaskCollection } from './task-response-normalizers';
import { TASK_STATUS } from './task.interfaces';

test('normalizes paged task collections and preserves customFields payloads', () => {
  const tasks = normalizeTaskCollection({
    items: [
      {
        id: 7,
        taskType: 'Research',
        currentStatus: 'not-a-number',
        assignedToUserId: 4,
        assignedToUser: {
          id: 4,
          name: 'Dana',
          email: 'dana@example.com',
          createdAt: '2026-05-20T00:00:00.000Z'
        },
        description: 'Investigate metadata workflow',
        customFields: {
          estimate: 3,
          tags: ['metadata', 'workflow']
        },
        createdAt: '2026-05-21T00:00:00.000Z',
        updatedAt: 42
      }
    ],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1
  });

  assert.equal(tasks.length, 1);
  assert.equal(tasks[0].currentStatus, TASK_STATUS.IN_PROGRESS);
  assert.equal(tasks[0].customDataJson, JSON.stringify({ estimate: 3, tags: ['metadata', 'workflow'] }));
  assert.equal(tasks[0].updatedAt, new Date(0).toISOString());
  assert.deepEqual(tasks[0].assignedToUser, {
    id: 4,
    name: 'Dana',
    email: 'dana@example.com',
    createdAt: '2026-05-20T00:00:00.000Z'
  });
});

test('normalizes workflow responses using the normalized task as the status fallback', () => {
  const response = normalizeChangeStatusResponse({
    success: true,
    message: 'Moved',
    newStatus: 'invalid',
    task: {
      id: 11,
      taskType: 'Development',
      currentStatus: TASK_STATUS.DONE,
      assignedToUserId: 1,
      description: 'Build metadata-driven board',
      customDataJson: '{"branchName":"feature/task-types"}',
      createdAt: '2026-05-22T00:00:00.000Z',
      updatedAt: '2026-05-23T00:00:00.000Z'
    }
  });

  assert.equal(response.success, true);
  assert.equal(response.newStatus, TASK_STATUS.DONE);
  assert.equal(response.task.customDataJson, '{"branchName":"feature/task-types"}');
});
