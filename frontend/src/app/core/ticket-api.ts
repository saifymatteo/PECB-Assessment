import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  Agent,
  PagedResult,
  TicketComment,
  TicketDetail,
  TicketListQuery,
  TicketListItem,
  TicketStatus,
  TicketWriteModel,
} from './models';

/** All ticket HTTP access lives here — components never touch HttpClient. */
@Injectable({ providedIn: 'root' })
export class TicketApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/tickets';

  list(query: TicketListQuery): Observable<PagedResult<TicketListItem>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize);

    if (query.search) params = params.set('search', query.search);
    if (query.status) params = params.set('status', query.status);
    if (query.priority) params = params.set('priority', query.priority);
    if (query.agentId) params = params.set('agentId', query.agentId);
    if (query.overdue) params = params.set('overdue', 'true');

    return this.http.get<PagedResult<TicketListItem>>(this.base, { params });
  }

  get(id: string): Observable<TicketDetail> {
    return this.http.get<TicketDetail>(`${this.base}/${id}`);
  }

  create(model: TicketWriteModel): Observable<TicketDetail> {
    return this.http.post<TicketDetail>(this.base, model);
  }

  update(id: string, model: TicketWriteModel): Observable<TicketDetail> {
    return this.http.put<TicketDetail>(`${this.base}/${id}`, model);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /** The only way a status changes (server validates the transition). */
  changeStatus(id: string, status: TicketStatus): Observable<TicketDetail> {
    return this.http.post<TicketDetail>(`${this.base}/${id}/status`, { status });
  }

  /** agentId === null unassigns. Server rejects inactive agents. */
  assignAgent(id: string, agentId: string | null): Observable<TicketDetail> {
    return this.http.put<TicketDetail>(`${this.base}/${id}/assignment`, { agentId });
  }

  addComment(id: string, authorName: string, body: string): Observable<TicketComment> {
    return this.http.post<TicketComment>(`${this.base}/${id}/comments`, { authorName, body });
  }
}

/** All agent HTTP access lives here. */
@Injectable({ providedIn: 'root' })
export class AgentApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/agents';

  list(search?: string): Observable<Agent[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<Agent[]>(this.base, { params });
  }

  setActive(id: string, active: boolean): Observable<Agent> {
    return this.http.put<Agent>(`${this.base}/${id}/status`, { active });
  }
}

export type { PagedResult };
