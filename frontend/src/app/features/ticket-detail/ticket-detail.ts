import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { extractApiError } from '../../core/api-error';
import { Agent, TicketDetail, TicketStatus } from '../../core/models';
import { PRIORITY_LABELS, STATUS_LABELS, transitionLabel } from '../../core/ticket-labels';
import { AgentApi, TicketApi } from '../../core/ticket-api';
import { TicketDialog, TicketDialogData } from '../ticket-dialog/ticket-dialog';

@Component({
  selector: 'app-ticket-detail',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatTooltipModule,
  ],
  templateUrl: './ticket-detail.html',
  styleUrl: './ticket-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketDetailPage implements OnInit {
  private readonly api = inject(TicketApi);
  private readonly agentApi = inject(AgentApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly statusLabels = STATUS_LABELS;
  readonly priorityLabels = PRIORITY_LABELS;

  readonly ticket = signal<TicketDetail | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly saving = signal(false);
  readonly agents = signal<Agent[]>([]);

  readonly commentForm = new FormGroup({
    authorName: new FormControl('', { validators: [Validators.required, Validators.maxLength(200)], nonNullable: true }),
    body: new FormControl('', { validators: [Validators.required, Validators.maxLength(4000)], nonNullable: true }),
  });

  readonly assignee = new FormControl<string | null>(null);

  readonly isClosed = computed(() => this.ticket()?.status === 'Closed');
  readonly isOverdue = computed(() => this.ticket()?.isOverdue ?? false);

  private readonly ticketId = this.route.snapshot.paramMap.get('id') ?? '';

  constructor() {
    this.assignee.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(agentId => this.onAssigneeChange(agentId));

    // A closed ticket is read-only: no comments, no assignment changes (rule 5).
    effect(() => {
      if (this.isClosed()) {
        this.commentForm.disable();
        this.assignee.disable();
      } else {
        this.commentForm.enable();
        this.assignee.enable();
      }
    });
  }

  ngOnInit(): void {
    this.agentApi.list().subscribe(all => this.agents.set(all.filter(a => a.active)));
    this.load();
  }

  /** Status transition buttons are rendered from the server-provided allowedTransitions. */
  transitionLabelFor(target: TicketStatus): string {
    return transitionLabel(target, this.ticket()!.status);
  }

  changeStatus(target: TicketStatus): void {
    const ticket = this.ticket();
    if (!ticket) return;

    this.saving.set(true);
    this.api.changeStatus(ticket.id, target).subscribe({
      next: updated => {
        this.saving.set(false);
        this.ticket.set(updated);
        this.snackBar.open(`Status changed to “${STATUS_LABELS[updated.status]}”.`, undefined, { duration: 3000 });
      },
      error: error => {
        this.saving.set(false);
        this.snackBar.open(extractApiError(error).message, 'Dismiss', { duration: 6000 });
        this.load(); // refresh in case the server state moved on
      },
    });
  }

  onAssigneeChange(agentId: string | null): void {
    const ticket = this.ticket();
    if (!ticket || agentId === (ticket.assignedAgentId ?? null)) return;

    this.saving.set(true);
    this.api.assignAgent(ticket.id, agentId).subscribe({
      next: updated => {
        this.saving.set(false);
        this.ticket.set(updated);
        this.snackBar.open(agentId ? 'Agent assigned.' : 'Agent unassigned.', undefined, { duration: 3000 });
      },
      error: error => {
        this.saving.set(false);
        this.snackBar.open(extractApiError(error).message, 'Dismiss', { duration: 6000 });
        this.assignee.setValue(ticket.assignedAgentId, { emitEvent: false });
      },
    });
  }

  openEdit(): void {
    const ticket = this.ticket();
    if (!ticket) return;

    this.dialog
      .open<TicketDialog, TicketDialogData, TicketDetail>(TicketDialog, {
        data: { mode: 'edit', ticket },
        width: '560px',
      })
      .afterClosed()
      .subscribe(updated => updated && this.ticket.set(updated));
  }

  confirmDelete(): void {
    const ticket = this.ticket();
    if (!ticket) return;

    this.dialog
      .open(DeleteConfirmDialog, { data: ticket.reference, width: '420px' })
      .afterClosed()
      .subscribe(confirmed => {
        if (!confirmed) return;
        this.api.delete(ticket.id).subscribe({
          next: () => {
            this.snackBar.open(`Ticket ${ticket.reference} deleted.`, undefined, { duration: 4000 });
            void this.router.navigate(['/']);
          },
          error: error => this.snackBar.open(extractApiError(error).message, undefined, { duration: 6000 }),
        });
      });
  }

  addComment(): void {
    if (this.commentForm.invalid) {
      this.commentForm.markAllAsTouched();
      return;
    }

    const ticket = this.ticket();
    if (!ticket) return;

    const { authorName, body } = this.commentForm.getRawValue();
    this.saving.set(true);
    this.api.addComment(ticket.id, authorName, body).subscribe({
      next: comment => {
        this.saving.set(false);
        this.commentForm.controls.body.reset('');
        this.ticket.set({ ...ticket, comments: [...ticket.comments, comment] });
      },
      error: error => {
        this.saving.set(false);
        this.snackBar.open(extractApiError(error).message, undefined, { duration: 6000 });
      },
    });
  }

  private load(): void {
    this.loading.set(true);
    this.notFound.set(false);

    this.api.get(this.ticketId).subscribe({
      next: ticket => {
        this.ticket.set(ticket);
        this.loading.set(false);
        this.assignee.setValue(ticket.assignedAgentId, { emitEvent: false });
      },
      error: () => {
        this.loading.set(false);
        this.notFound.set(true);
      },
    });
  }
}

/** Minimal confirmation dialog for ticket deletion. */
@Component({
  selector: 'app-delete-confirm',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Delete ticket {{ data }}?</h2>
    <mat-dialog-content>
      <p>This permanently removes the ticket and its comments. This cannot be undone.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="warn" [mat-dialog-close]="true">Delete</button>
    </mat-dialog-actions>
  `,
})
export class DeleteConfirmDialog {
  readonly data = inject<string>(MAT_DIALOG_DATA);
}
