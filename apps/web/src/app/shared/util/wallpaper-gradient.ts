/**
 * Gradientes Tidal usados enquanto a miniatura não carrega (ou quando ela falha). Cada wallpaper recebe
 * sempre o mesmo gradiente, calculado a partir do id, então a grade não "pisca" entre carregamentos.
 */
export const WALLPAPER_GRADIENTS: readonly string[] = [
  'radial-gradient(ellipse 120% 120% at 20% 15%, #3e9be0 0%, #1e5fb0 55%, #0a2e66 100%)',
  'linear-gradient(145deg, #2e7fd6 0%, #3e5fd0 60%, #2ea8c8 100%)',
  'radial-gradient(ellipse 130% 130% at 80% 20%, #4ab6d6 0%, #1e6fc0 50%, #0a2e66 100%)',
  'linear-gradient(160deg, #cfe3f2 0%, #9ec2e6 45%, #b8d4ec 100%)',
  'conic-gradient(from 210deg at 70% 30%, #0a4d8c 0%, #2e7fd6 33%, #2ec8c8 67%, #0a4d8c 100%)',
  'linear-gradient(145deg, #0a2a5a 0%, #1e5fb0 50%, #3ea8e0 100%)',
];

/** Hash djb2: simples, estável e suficiente para espalhar ids entre poucos gradientes. */
function hash(text: string): number {
  let value = 5381;
  for (let index = 0; index < text.length; index++) {
    value = ((value << 5) + value + text.charCodeAt(index)) >>> 0;
  }
  return value;
}

export function gradientFor(wallpaperId: string): string {
  return WALLPAPER_GRADIENTS[hash(wallpaperId) % WALLPAPER_GRADIENTS.length];
}
