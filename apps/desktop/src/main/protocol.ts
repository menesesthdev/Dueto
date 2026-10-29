import { protocol, net } from 'electron';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { getApiBaseUrl, PROTOCOL_SCHEME, WEB_DIST_PATH } from './config.js';

const CONTENT_TYPES: Record<string, string> = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.woff2': 'font/woff2',
  '.woff': 'font/woff',
  '.ico': 'image/x-icon',
  '.webp': 'image/webp',
  '.webmanifest': 'application/manifest+json',
};

export function registerProtocolPrivileges(): void {
  protocol.registerSchemesAsPrivileged([
    {
      scheme: PROTOCOL_SCHEME,
      privileges: {
        standard: true,
        secure: true,
        supportFetchAPI: true,
        corsEnabled: true,
        stream: true,
      },
    },
  ]);
}

export function registerProtocol(): void {
  protocol.handle(PROTOCOL_SCHEME, async (request) => {
    const url = new URL(request.url);

    if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hub/')) {
      return proxyToApi(request, url);
    }

    return serveStaticFile(url.pathname);
  });
}

async function proxyToApi(request: Request, url: URL): Promise<Response> {
  const target = `${getApiBaseUrl()}${url.pathname}${url.search}`;
  const headers = new Headers(request.headers);
  headers.delete('host');

  const hasBody = request.method !== 'GET' && request.method !== 'HEAD';
  return net.fetch(target, {
    method: request.method,
    headers,
    body: hasBody ? request.body : undefined,
    ...(hasBody ? { duplex: 'half' } : {}),
  } as RequestInit);
}

async function serveStaticFile(pathname: string): Promise<Response> {
  const webDist = path.resolve(WEB_DIST_PATH());
  const relative = decodeURIComponent(pathname === '/' ? '/index.html' : pathname);
  const filePath = path.resolve(webDist, `.${relative}`);

  if (filePath !== webDist && !filePath.startsWith(webDist + path.sep)) {
    return new Response('Forbidden', { status: 403 });
  }

  try {
    const data = await readFile(filePath);
    const contentType = CONTENT_TYPES[path.extname(filePath)] ?? 'application/octet-stream';
    return new Response(data, { headers: { 'Content-Type': contentType } });
  } catch {
    if (path.extname(pathname)) {
      return new Response('Not found', { status: 404 });
    }
    try {
      const data = await readFile(path.join(webDist, 'index.html'));
      return new Response(data, { headers: { 'Content-Type': CONTENT_TYPES['.html'] } });
    } catch {
      return new Response('Not found', { status: 404 });
    }
  }
}
