import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_BASE_URL } from '../http/api-base-url';
import type { SetStateResponse, StateSnapshot } from './models';

@Injectable({ providedIn: 'root' })
export class StateApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  get(): Observable<StateSnapshot> {
    return this.http.get<StateSnapshot>(`${this.base}/api/state`);
  }

  set(wallpaperId: string): Observable<SetStateResponse> {
    return this.http.put<SetStateResponse>(`${this.base}/api/state`, { wallpaperId });
  }
}
