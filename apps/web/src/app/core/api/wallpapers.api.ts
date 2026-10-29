import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_BASE_URL } from '../http/api-base-url';
import type { Wallpaper } from './models';

@Injectable({ providedIn: 'root' })
export class WallpapersApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  list(): Observable<Wallpaper[]> {
    return this.http.get<Wallpaper[]>(`${this.base}/api/wallpapers`);
  }

  get(id: string): Observable<Wallpaper> {
    return this.http.get<Wallpaper>(`${this.base}/api/wallpapers/${encodeURIComponent(id)}`);
  }
}
