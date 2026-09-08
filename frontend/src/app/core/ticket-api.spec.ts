import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { PagedResult, TicketListItem } from './models';
import { AgentApi, TicketApi } from './ticket-api';

describe('TicketApi', () => {
  let api: TicketApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    api = TestBed.inject(TicketApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends server-side search, filters and pagination as query parameters', () => {
    let result: PagedResult<TicketListItem> | undefined;

    api
      .list({
        page: 2,
        pageSize: 10,
        search: 'refund',
        status: 'Resolved',
        priority: 'High',
        agentId: 'abc',
        overdue: true,
      })
      .subscribe(r => (result = r));

    const req = http.expectOne(
      r => r.url === '/api/tickets' && r.params.get('page') === '2',
    );
    expect(req.request.params.get('pageSize')).toBe('10');
    expect(req.request.params.get('search')).toBe('refund');
    expect(req.request.params.get('status')).toBe('Resolved');
    expect(req.request.params.get('priority')).toBe('High');
    expect(req.request.params.get('agentId')).toBe('abc');
    expect(req.request.params.get('overdue')).toBe('true');

    req.flush({ items: [], page: 2, pageSize: 10, totalItems: 0, totalPages: 0 });
    expect(result?.items).toEqual([]);
  });

  it('omits empty filters instead of sending blank query parameters', () => {
    api.list({ page: 1, pageSize: 20 }).subscribe();

    const req = http.expectOne(r => r.url === '/api/tickets');
    expect(req.request.params.keys()).toEqual(['page', 'pageSize']);
    req.flush({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 });
  });

  it('changes status via the dedicated endpoint', () => {
    api.changeStatus('ticket-1', 'InProgress').subscribe();

    const req = http.expectOne('/api/tickets/ticket-1/status');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ status: 'InProgress' });
    req.flush({});
  });

  it('unassigns an agent by sending a null agentId', () => {
    api.assignAgent('ticket-1', null).subscribe();

    const req = http.expectOne('/api/tickets/ticket-1/assignment');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ agentId: null });
    req.flush({});
  });
});

describe('AgentApi', () => {
  let api: AgentApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    api = TestBed.inject(AgentApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists agents, passing search through to the server', () => {
    api.list('dana').subscribe();

    const req = http.expectOne(r => r.url === '/api/agents');
    expect(req.request.params.get('search')).toBe('dana');
    req.flush([]);
  });

  it('toggles agent activity through the status endpoint', () => {
    api.setActive('agent-1', false).subscribe();

    const req = http.expectOne('/api/agents/agent-1/status');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ active: false });
    req.flush({});
  });
});
