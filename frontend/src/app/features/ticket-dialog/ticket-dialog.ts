import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';

import { extractApiError } from '../../core/api-error';
import { TicketDetail, TicketPriority } from '../../core/models';
import { PRIORITY_LABELS } from '../../core/ticket-labels';
import { TicketApi } from '../../core/ticket-api';

export interface TicketDialogData {
  mode: 'create' | 'edit';
  ticket?: TicketDetail;
}

/**
 * Create / edit ticket dialog. Reactive Forms with client-side validation;
 * server-side rule violations are surfaced in a readable way below the form.
 */
@Component({
  selector: 'app-ticket-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './ticket-dialog.html',
  styleUrl: './ticket-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketDialog {
  private readonly api = inject(TicketApi);
  private readonly dialogRef = inject(MatDialogRef<TicketDialog>);
  readonly data = inject<TicketDialogData>(MAT_DIALOG_DATA);

  readonly priorities = Object.entries(PRIORITY_LABELS) as [TicketPriority, string][];
  readonly saving = signal(false);
  readonly serverError = signal('');

  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    title: [this.data.ticket?.title ?? '', [Validators.required, Validators.maxLength(200)]],
    description: [this.data.ticket?.description ?? '', [Validators.required, Validators.maxLength(4000)]],
    customerName: [this.data.ticket?.customerName ?? '', [Validators.required, Validators.maxLength(200)]],
    customerEmail: [
      this.data.ticket?.customerEmail ?? '',
      [Validators.required, Validators.email, Validators.maxLength(320)],
    ],
    priority: [this.data.ticket?.priority ?? ('Normal' satisfies TicketPriority), Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.serverError.set('');
    const model = this.form.getRawValue();

    const request = this.data.mode === 'create'
      ? this.api.create(model)
      : this.api.update(this.data.ticket!.id, model);

    request.subscribe({
      next: ticket => this.dialogRef.close(ticket),
      error: error => {
        this.saving.set(false);
        this.serverError.set(extractApiError(error).message);
      },
    });
  }
}
