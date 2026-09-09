import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, merge, of, startWith, switchMap, tap, catchError, finalize } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { Agent, PagedResult, TicketListItem, TicketPriority, TicketStatus } from '../../core/models';
import { PRIORITY_LABELS, STATUS_LABELS } from '../../core/ticket-labels';
import { AgentApi, TicketApi } from '../../core/ticket-api';
import { TicketDialog, TicketDialogData } from '../ticket-dialog/ticket-dialog';

@Component({
  selector: 'app-ticket-list',
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: './ticket-list.html',
  styleUrl: './ticket-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketList implements OnInit {
  private readonly api = inject(TicketApi);
  private readonly agentApi = inject(AgentApi);
  private readonly dialog = inject(MatDialog);

  readonly displayedColumns = ['reference', 'title', 'customerName', 'priority', 'status', 'agent', 'dueDate'];
  readonly statusLabels = STATUS_LABELS;
  readonly priorityLabels = PRIORITY_LABELS;
  readonly statusOptions = Object.keys(STATUS_LABELS) as TicketStatus[];
  readonly priorityOptions = Object.keys(PRIORITY_LABELS) as TicketPriority[];

  // Debounced server-side search (RxJS): the API filters, never the client.
  readonly search = new FormControl('', { nonNullable: true });
  readonly status = new FormControl<TicketStatus | ''>('');
  readonly priority = new FormControl<TicketPriority | ''>('');
  readonly agentId = new FormControl<string>('');
  readonly overdueOnly = new FormControl(false, { nonNullable: true });

  readonly tickets = signal<TicketListItem[]>([]);
  readonly total = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);
  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly agents = signal<Agent[]>([]);

  private readonly refresh$ = new Subject<void>;

  constructor() {
    this.refresh$.pipe(
      startWith(undefined),
      tap(() => {
        this.loading.set(true);
        this.loadError.set('');
      }),
      switchMap(() =>
        this.api.list(this.buildQuery()).pipe(
          finalize(() => this.loading.set(false)),
          catchError(() => {
            this.loadError.set('Could not load tickets. Is the API running?');
            return of<PagedResult<TicketListItem> | null>(null);
          }),
        ),
      ),
      takeUntilDestroyed(),
    ).subscribe(result => {
      if (result) {
        this.tickets.set(result.items);
        this.total.set(result.totalItems);
      }
    });

    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => this.resetAndReload());

    merge(this.status.valueChanges, this.priority.valueChanges, this.agentId.valueChanges, this.overdueOnly.valueChanges)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.resetAndReload());
  }

  ngOnInit(): void {
    this.agentApi.list().subscribe(agents => this.agents.set(agents));
  }

  priorityLabel(priority: TicketPriority): string {
    return PRIORITY_LABELS[priority];
  }

  statusLabel(status: TicketStatus): string {
    return STATUS_LABELS[status];
  }

  /** Inactive agents keep their open-ticket assignments; mark them where they appear. */
  agentLabel(ticket: TicketListItem): string {
    if (!ticket.assignedAgentName) return '-';
    const agent = this.agents().find(a => a.id === ticket.assignedAgentId);
    return agent && !agent.active ? `${ticket.assignedAgentName} (inactive)` : ticket.assignedAgentName;
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.refresh$.next();
  }

  openCreate(): void {
    this.dialog
      .open<TicketDialog, TicketDialogData, unknown>(TicketDialog, { data: { mode: 'create' }, width: '560px' })
      .afterClosed()
      .subscribe(created => created && this.refresh$.next());
  }

  private resetAndReload(): void {
    this.pageIndex.set(0);
    this.refresh$.next();
  }

  private buildQuery() {
    return {
      page: this.pageIndex() + 1,
      pageSize: this.pageSize(),
      search: this.search.value.trim() || undefined,
      status: (this.status.value || undefined) as TicketStatus | undefined,
      priority: (this.priority.value || undefined) as TicketPriority | undefined,
      agentId: this.agentId.value || undefined,
      overdue: this.overdueOnly.value || undefined,
    };
  }
}
