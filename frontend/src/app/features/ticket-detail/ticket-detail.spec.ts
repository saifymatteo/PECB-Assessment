import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { MatOption } from '@angular/material/core';

import { Agent, TicketDetail } from '../../core/models';
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
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: (key: string) => (key === 'id' ? 'ticket-1' : null) } } },
        },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function createAndFlush(ticket: TicketDetail, agents: Agent[] = []): ComponentFixture<TicketDetailPage> {
    const fixture = TestBed.createComponent(TicketDetailPage);
    fixture.detectChanges();
    http.expectOne('/api/agents').flush(agents);
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

  it('renders the All tickets back-link as a real link (RouterLink imported)', () => {
    const fixture = createAndFlush(detailFixture());

    const back = fixture.nativeElement.querySelector('a.back-link') as HTMLAnchorElement;
    expect(back).withContext('back link rendered').toBeTruthy();
    expect(back.getAttribute('href')).withContext('href from routerLink').toBe('/');
  });

  it('filters inactive agents out of the assignment picker (server still enforces rule 4)', () => {
    const fixture = createAndFlush(detailFixture(), [
      { id: 'agent-1', fullName: 'Dana Whitfield', email: 'dana@example.com', department: 'Technical', active: true },
      { id: 'agent-2', fullName: 'Priya Nair', email: 'priya@example.com', department: 'General', active: false },
    ]);

    const trigger = fixture.nativeElement.querySelector('.mat-mdc-select-trigger') as HTMLElement;
    trigger.click();
    fixture.detectChanges();

    const optionLabels = Array.from(document.querySelectorAll('.mat-mdc-select-panel mat-option')).map(
      o => o.textContent?.trim(),
    );
    expect(optionLabels).toEqual(['Unassigned', 'Dana Whitfield']);
  });

  it('filters inactive agents out of the assignment picker (server still enforces rule 4)', () => {
    const fixture = createAndFlush(detailFixture(), [
      { id: 'agent-1', fullName: 'Dana Whitfield', email: 'dana@example.com', department: 'Technical', active: true },
      { id: 'agent-2', fullName: 'Priya Nair', email: 'priya@example.com', department: 'General', active: false },
    ]);

    const trigger = fixture.nativeElement.querySelector('.mat-mdc-select-trigger') as HTMLElement;
    trigger.click();
    fixture.detectChanges();

    const optionLabels = Array.from(document.querySelectorAll('.mat-mdc-select-panel mat-option')).map(
      o => o.textContent?.trim(),
    );
    expect(optionLabels).toEqual(['Unassigned', 'Dana Whitfield']);
  });

  it('shows the current assignee as a read-only (inactive) entry when they are deactivated', async () => {
    const fixture = createAndFlush(
      detailFixture({ assignedAgentId: 'agent-2', assignedAgentName: 'Priya Nair' }),
      [
        {
          id: 'agent-1',
          fullName: 'Dana Whitfield',
          email: 'dana@example.com',
          department: 'Technical',
          active: true,
        },
      ],
    );

    await fixture.whenStable();
    fixture.detectChanges();

    // The trigger reflects the still-intact assignment instead of looking unassigned.
    const trigger = fixture.nativeElement.querySelector('.mat-mdc-select-trigger') as HTMLElement;
    expect(trigger.textContent).toContain('Priya Nair (inactive)');

    trigger.click();
    fixture.detectChanges();

    const options = fixture.debugElement.queryAll(By.directive(MatOption));
    const inactive = options.find(o => o.nativeElement.textContent.includes('Priya Nair (inactive)'));
    expect(inactive).withContext('inactive assignee option rendered').toBeTruthy();
    expect(inactive!.componentInstance.disabled).toBeTrue();
  });
});
