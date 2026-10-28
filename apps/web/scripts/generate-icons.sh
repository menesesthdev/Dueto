#!/usr/bin/env bash
# Gera os ícones do PWA a partir dos SVGs em design/. Requer rsvg-convert e ImageMagick (convert).
# Rode em apps/web:  ./scripts/generate-icons.sh
set -euo pipefail
cd "$(dirname "$0")/.."

out=public/icons
mkdir -p "$out"
rm -f "$out"/*.png

# "any": cantos arredondados e fundo transparente (instalação no desktop e Android).
rsvg-convert -w 192 -h 192 design/icon-any.svg -o "$out/icon-192.png"
rsvg-convert -w 512 -h 512 design/icon-any.svg -o "$out/icon-512.png"

# "maskable": sangra até a borda; o sistema aplica a máscara, e o símbolo fica dentro da zona segura.
rsvg-convert -w 192 -h 192 design/icon-full.svg -o "$out/icon-maskable-192.png"
rsvg-convert -w 512 -h 512 design/icon-full.svg -o "$out/icon-maskable-512.png"

# iOS: o iPhone aplica a própria máscara e pinta cantos transparentes de preto, por isso sem transparência.
rsvg-convert -w 180 -h 180 design/icon-full.svg -o "$out/apple-touch-icon.png"

# Favicon: versão "mini" (só as setas), porque o símbolo completo embola em 16 px.
cp design/favicon.svg public/favicon.svg
for size in 16 32 48; do rsvg-convert -w "$size" -h "$size" design/favicon.svg -o "/tmp/dueto-favicon-$size.png"; done
magick /tmp/dueto-favicon-16.png /tmp/dueto-favicon-32.png /tmp/dueto-favicon-48.png public/favicon.ico
rm -f /tmp/dueto-favicon-*.png
