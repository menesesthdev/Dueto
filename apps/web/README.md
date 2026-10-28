# apps/web — PWA do Dueto (Angular)

O mesmo app roda no navegador, instalado como PWA no iPhone e dentro do Electron no desktop (Fase 4).
O tipo de aparelho é detectado pelo `PlatformService`: o desktop **gera** o código de pareamento e o celular **digita**.

## Rodar em desenvolvimento

Pré-requisitos: a API rodando em `http://localhost:5183` (veja o `README` da raiz) e Node 24 (ver `.tool-versions`).

| O quê | Pasta | Comando | Conferir |
|---|---|---|---|
| Instalar dependências | `apps/web` | `npm ci` | termina sem erro |
| Subir o PWA | `apps/web` | `npm start` | `http://localhost:4200` abre a tela de pareamento |
| Testes | `apps/web` | `npm test -- --watch=false` | todos passam |
| Lint | `apps/web` | `npx ng lint` | `All files pass linting.` |
| Build de produção | `apps/web` | `npx ng build` | gera `dist/web/browser` com `ngsw-worker.js` |
| Regerar ícones | `apps/web` | `./scripts/generate-icons.sh` | PNGs novos em `public/icons` (requer `rsvg-convert` e `magick`) |

O `ng serve` encaminha `/api` e `/hub` para a API local (`proxy.conf.json`), então não é preciso configurar CORS em desenvolvimento.
Para a API ter wallpapers, suba-a com o catálogo de exemplo (`Catalog__ImportFile=../../samples/catalog.sample.json` e
`Catalog__AllowedHosts__0=example.com`).

Para ver o layout de celular no computador, use o modo de dispositivo do navegador com um iPhone: a detecção usa o *user agent*.

## Estrutura

```
src/
├── styles/            tokens Tidal (_tokens.scss), fonte e estilos base
├── environments/      apiBaseUrl (vazio = mesma origem, atrás do Caddy)
└── app/
    ├── core/
    │   ├── api/       contratos (models.ts) e clientes HTTP da API
    │   ├── http/      interceptor do token e URL base
    │   ├── platform/  PlatformService (desktop, celular ou Electron)
    │   └── session/   token do aparelho (localStorage) e guards de rota
    ├── shared/        ícones, logo, miniatura, utilitários
    └── features/
        ├── pairing/   telas de pareamento (gerar e digitar o código)
        └── catalog/   catálogo (layout de desktop e de celular)
```

## Decisões

- **Design:** telas do Paper (arquivo "Dueto", página "Hub — Protótipo"), paleta **Tidal**. Os tokens do arquivo do Paper ainda têm o
  violeta antigo; os valores certos estão em `src/styles/_tokens.scss`.
- **Fonte:** SF Pro não pode ser distribuída na web. A pilha usa a fonte do sistema da Apple quando existe e a Inter (OFL, só o subconjunto
  latino, ~48 kB) nos outros aparelhos.
- **Texto:** "PC", nunca "Mac" (o desktop é Windows primeiro). Favoritos, Coleções e a resolução dos arquivos ficaram de fora: a API não tem.
- **Sem conta:** o token do aparelho fica no `localStorage`. Um `401` apaga a sessão e volta ao pareamento.
- **Ainda não feito (Fase 4/5):** tempo real pelo SignalR, banner de pendência, rota `/apply`, Atalho do iOS e Web Push.
