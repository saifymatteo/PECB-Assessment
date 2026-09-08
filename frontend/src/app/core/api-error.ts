import { HttpErrorResponse } from '@angular/common/http';

/** RFC 7807 ProblemDetails (optionally ValidationProblemDetails) as served by the API. */
export interface ApiProblem {
  title?: string;
  status?: number;
  detail?: string;
  errorCode?: string;
  allowedTransitions?: string[];
  errors?: Record<string, string[]>;
}

export interface ApiError {
  message: string;
  fieldErrors: Record<string, string>;
}

/**
 * Turns an HttpErrorResponse carrying ProblemDetails into something displayable:
 * a human message plus per-field messages for form binding.
 */
export function extractApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { message: 'Something went wrong. Please try again.', fieldErrors: {} };
  }

  const problem = error.error as ApiProblem | null;

  if (problem && typeof problem === 'object' && (problem.title || problem.errors)) {
    const fieldErrors: Record<string, string> = {};
    for (const [key, messages] of Object.entries(problem.errors ?? {})) {
      // JSON binding problems arrive as "$.priority"; strip to the plain field name.
      fieldErrors[key.replace(/^\$\./, '')] = messages.join(' ');
    }
    const message = problem.detail
      ?? (problem.errorCode === 'INVALID_TRANSITION'
        ? `${problem.title ?? 'Invalid transition'}. Allowed: ${(problem.allowedTransitions ?? []).join(', ')}`
        : problem.title ?? 'Request failed');
    return { message, fieldErrors };
  }

  return {
    message: error.status === 0 ? 'Cannot reach the server. Is the API running?' : `Request failed (${error.status}).`,
    fieldErrors: {},
  };
}
