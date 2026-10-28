import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_BASE_URL } from '../http/api-base-url';
import type {
  CompletePairingRequest,
  CompletePairingResponse,
  StartPairingRequest,
  StartPairingResponse,
} from './models';

@Injectable({ providedIn: 'root' })
export class PairingApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  start(request: StartPairingRequest): Observable<StartPairingResponse> {
    return this.http.post<StartPairingResponse>(`${this.base}/api/pairing/start`, request);
  }

  complete(request: CompletePairingRequest): Observable<CompletePairingResponse> {
    return this.http.post<CompletePairingResponse>(`${this.base}/api/pairing/complete`, request);
  }
}
