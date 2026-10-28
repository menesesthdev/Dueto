import { HttpErrorResponse } from '@angular/common/http';
import type { ProblemDetails } from './models';

export interface ApiErrorInfo {
  /** Código HTTP, ou nulo se a requisição nem chegou ao servidor (rede). */
  status: number | null;
  /** Título do ProblemDetails, quando houver. */
  title: string | null;
}

/** Extrai o essencial de um erro HTTP sem expor detalhes técnicos ao usuário. */
export function describeError(error: unknown): ApiErrorInfo {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as ProblemDetails | null;
    return {
      status: error.status === 0 ? null : error.status,
      title: typeof problem?.title === 'string' ? problem.title : null,
    };
  }

  return { status: null, title: null };
}
