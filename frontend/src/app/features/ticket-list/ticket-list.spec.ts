import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { Agent, PagedResult, TicketListItem } from '../../core/models';
import { TicketList } from './ticket-list';

function itemFixture(overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
    id: 'ticket-1',
    reference: 'TCK-2026-0011',
    title: 'Email notifications duplicated',
    customerName: 'Julia Moreau',
    priority: 'Normal',
    status: 'InProgress',
    assignedAgentId: null,
    assignedAgentName: null,
    createdAt: '2026-01-10T09:00:00Z',
    dueDate: '2026-01-12T09:00:00Z',
    isOverdue: false,
    ...overrides,
  };
}

describe('TicketList', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TicketList],
      providers: [
        provideHttpClient(withFetch()),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function createAndFlush(items: TicketListItem[]): ComponentFixture<TicketList> {
    const fixture = TestBed.createComponent(TicketList);
    fixture.detectChanges();
    http.expectOne('/api/agents').flush([
      { id: 'agent-1', fullName: 'Dana Whitfield', email: 'dana@example.com', department: 'Technical', active: true },
      { id: 'agent-2', fullName: 'Priya Nair', email: 'priya@example.com', department: 'General', active: false },
    ] satisfies Agent[]);
    http.expectOne('/api/tickets?page=1&pageSize=20').flush({
      items,
      page: 1,
      pageSize: 20,
      totalItems: items.length,
      totalPages: 1,
    } satisfies PagedResult<TicketListItem>);
    fixture.detectChanges();
    return fixture;
  }

  it('marks inactive assignees in the Agent column, plain name for active, dash for unassigned', () => {
    const fixture = createAndFlush([
      itemFixture({ id: 't1', assignedAgentId: 'agent-2', assignedAgentName: 'Priya Nair' }),
      itemFixture({ id: 't2', assignedAgentId: 'agent-1', assignedAgentName: 'Dana Whitfield' }),
      itemFixture({ id: 't3', assignedAgentId: null, assignedAgentName: null }),
    ]);

    const cells = Array.from(
      fixture.nativeElement.querySelectorAll('td.mat-column-agent') as NodeListOf<HTMLElement>,
    ).map(c => c.textContent?.trim());
    expect(cells).toEqual(['Priya Nair (inactive)', 'Dana Whitfield', '-']);
  });
});
