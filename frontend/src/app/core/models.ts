/** API models — mirrored from the ASP.NET Core contracts (kebab-cased to camelCase). */

export type TicketStatus = 'New' | 'InProgress' | 'Resolved' | 'Closed';
export type TicketPriority = 'Low' | 'Normal' | 'High' | 'Critical';
export type AgentDepartment = 'Technical' | 'Billing' | 'General';

export interface TicketComment {
  id: string;
  authorName: string;
  body: string;
  createdAt: string;
}

export interface TicketListItem {
  id: string;
  reference: string;
  title: string;
  customerName: string;
  priority: TicketPriority;
  status: TicketStatus;
  assignedAgentId: string | null;
  assignedAgentName: string | null;
  createdAt: string;
  dueDate: string;
  isOverdue: boolean;
}

export interface TicketDetail extends TicketListItem {
  description: string;
  customerEmail: string;
  lastModifiedAt: string;
  resolvedAt: string | null;
  closedAt: string | null;
  /** Server truth about which transitions are legal right now; the UI renders only these. */
  allowedTransitions: TicketStatus[];
  comments: TicketComment[];
}

export interface TicketWriteModel {
  title: string;
  description: string;
  customerName: string;
  customerEmail: string;
  priority: TicketPriority;
}

export interface TicketListQuery {
  page: number;
  pageSize: number;
  search?: string;
  status?: TicketStatus;
  priority?: TicketPriority;
  agentId?: string;
  overdue?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface Agent {
  id: string;
  fullName: string;
  email: string;
  department: AgentDepartment;
  active: boolean;
}
