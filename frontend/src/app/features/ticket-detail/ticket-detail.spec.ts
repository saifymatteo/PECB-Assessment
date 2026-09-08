import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute } from '@angular/router';

import { TicketDetail } from '../../core/models';
import { TicketDetailPage } from './ticket-detail';

function detailFixture(overrides: Partial<TicketDetail> = {}): TicketDetail {
  return {
    id: 'ticket-1',
    reference: 'TCK-2026-0001',
    title: 'Printer on fire',
    description: 'Smells bad',
    customerName: 'Alice',
    customerEmail: 'alice@example.com',
    priority: 'Critical',
    status: 'New',
    assignedAgentId: null,
    assignedAgentName: null,
    createdAt: '2026-01-10T09:00:00Z',
    lastModifiedAt: '2026-01-10T09:00:00Z',
    resolvedAt: null,
    closedAt: null,
    dueDate: '2026-01-10T13:00:00Z',
    isOverdue: false,
    allowedTransitions: ['InProgress'],
    comments: [],
    ...overrides,
  };
}

describe('TicketDetailPage', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TicketDetailPage],
      providers: [
        provideHttpClient(withFetch()),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: (key: string) => (key === 'id' ? 'ticket-1' : null) } } },
        },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function createAndFlush(ticket: TicketDetail): ComponentFixture<TicketDetailPage> {
    const fixture = TestBed.createComponent(TicketDetailPage);
    fixture.detectChanges();
    http.expectOne('/api/agents').flush([]);
    http.expectOne('/api/tickets/ticket-1').flush(ticket);
    fixture.detectChanges();
    return fixture;
  }

  function allButtons(fixture: ComponentFixture<TicketDetailPage>): HTMLButtonElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>);
  }

  it('renders only the transitions the API allows for the current status', () => {
    const fixture = createAndFlush(
      detailFixture({ status: 'Resolved', allowedTransitions: ['Closed', 'InProgress'] }),
    );

    const labels = allButtons(fixture).map(b => b.textContent?.trim());
    expect(labels).toContain('Close ticket');
    expect(labels).toContain('Reopen');
    expect(labels).not.toContain('Mark resolved'); // an illegal move is never offered
    expect(labels).not.toContain('Start progress');
  });

  it('disables every action on a closed ticket (rule 5)', () => {
    const fixture = createAndFlush(
      detailFixture({ status: 'Closed', allowedTransitions: [], closedAt: '2026-01-11T09:00:00Z' }),
    );

    const buttons = allButtons(fixture);
    expect(buttons.length).toBeGreaterThan(0);
    expect(buttons.every(b => b.disabled)).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('read-only');
  });

  it('shows the overdue badge for an overdue open ticket', () => {
    const fixture = createAndFlush(
      detailFixture({ status: 'New', isOverdue: true, allowedTransitions: ['InProgress'] }),
    );

    expect(fixture.nativeElement.textContent).toContain('Overdue');
  });
});
