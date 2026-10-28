/**
 * Ambiente de desenvolvimento.
 *
 * Também usa a mesma origem: o `ng serve` encaminha /api e /hub para a API local (veja proxy.conf.json),
 * o que dispensa CORS em desenvolvimento.
 */
export const environment = {
  production: false,
  apiBaseUrl: '',
};
