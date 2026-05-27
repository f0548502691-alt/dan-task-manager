import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AppErrorService } from './core/app-error.service';
import { TaskWorkflowBoardComponent } from './tasks/task-workflow-board.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, TaskWorkflowBoardComponent],
  template: `
    <main class="app-shell">
      <header class="app-header">
        <h1>Task Workflow Board</h1>
      </header>

      <p class="global-error" *ngIf="appErrorService.error() as globalError">{{ globalError }}</p>
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

      .global-error {
        margin: 0 0 1rem;
        color: #b42318;
        font-weight: 600;
      }
    `
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent {
  readonly appErrorService = inject(AppErrorService);
}
