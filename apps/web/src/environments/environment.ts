/**
 * Ambiente de produção.
 *
 * `apiBaseUrl` vazio significa "mesma origem": em produção o Caddy serve o PWA e faz proxy de /api e /hub
 * para a API, então as chamadas são relativas. O app desktop (Electron) usa um build com a URL absoluta da API.
 */
export const environment = {
  production: true,
  apiBaseUrl: '',
};
