/** Contratos da API do Dueto (CLAUDE.md §8). Os nomes seguem o JSON em camelCase. */

export type DeviceKind = 'desktop' | 'mobile';
export type WallpaperKind = 'static' | 'animated';

export interface Wallpaper {
  id: string;
  title: string;
  kind: WallpaperKind;
  desktopUrl: string;
  desktopSha256: string;
  /** Nulo em wallpapers animados (só desktop). */
  mobileUrl: string | null;
  mobileSha256: string | null;
  thumbnailUrl: string;
  tags: string[];
  createdAt: string;
}

export interface StartPairingRequest {
  deviceName: string;
  kind: DeviceKind;
}

export interface StartPairingResponse {
  deviceToken: string;
  code: string;
  expiresAt: string;
}

export interface CompletePairingRequest {
  code: string;
  deviceName: string;
  kind: DeviceKind;
}

export interface CompletePairingResponse {
  deviceToken: string;
}

export interface DeviceApplied {
  deviceId: string;
  kind: DeviceKind;
  wallpaperId: string | null;
  version: number | null;
  appliedAt: string | null;
}

export interface StateSnapshot {
  wallpaperId: string | null;
  version: number;
  originDeviceId: string | null;
  applied: DeviceApplied[];
}

export interface SetStateResponse {
  version: number;
}

/** Erro no formato ProblemDetails (RFC 7807). */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
