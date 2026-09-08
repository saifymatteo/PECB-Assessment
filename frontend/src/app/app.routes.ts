import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/ticket-list/ticket-list').then(m => m.TicketList),
    title: 'Support Desk — Tickets',
  },
  {
    path: 'tickets/:id',
    loadComponent: () => import('./features/ticket-detail/ticket-detail').then(m => m.TicketDetailPage),
    title: 'Support Desk — Ticket',
  },
];
