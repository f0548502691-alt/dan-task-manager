import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TaskWorkflowBoardComponent } from './tasks/task-workflow-board.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [TaskWorkflowBoardComponent],
  template: `
    <main class="app-shell">
      <header class="app-header">
        <h1>Task Workflow Board</h1>
      </header>

      <app-task-workflow-board></app-task-workflow-board>
    </main>
  `,
  styles: [
    `
      .app-shell {
        max-width: 1440px;
        margin: 0 auto;
        padding: 1.5rem;
      }

      .app-header {
        margin-bottom: 1rem;
      }

      .app-header h1 {
        margin: 0;
        font-size: 1.5rem;
      }
    `
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent {}
