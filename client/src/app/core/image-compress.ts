// Comprime un'immagine lato client prima dell'upload del logo: ridimensiona al
// lato massimo e ri-codifica (WebP, fallback PNG) così che file pesanti (es. foto
// da telefono) rientrino nel limite server senza chiedere all'admin di ridurli a mano.
// Gli SVG passano invariati (vettoriali, già leggeri).

export interface CompressOptions {
  maxSide?: number; // lato massimo in px (default 512)
  quality?: number; // qualità WebP 0..1 (default 0.85)
}

export async function compressImage(file: File, opts: CompressOptions = {}): Promise<File> {
  const maxSide = opts.maxSide ?? 512;
  const quality = opts.quality ?? 0.85;

  // SVG: niente raster, restituisci com'è.
  if (file.type === 'image/svg+xml') return file;

  const bitmap = await loadBitmap(file);
  const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
  const w = Math.max(1, Math.round(bitmap.width * scale));
  const h = Math.max(1, Math.round(bitmap.height * scale));

  const canvas = document.createElement('canvas');
  canvas.width = w;
  canvas.height = h;
  const ctx = canvas.getContext('2d');
  if (!ctx) return file; // contesto non disponibile: meglio l'originale
  ctx.drawImage(bitmap, 0, 0, w, h);
  if ('close' in bitmap) (bitmap as ImageBitmap).close();

  // WebP preferito (più compatto); se il browser non lo supporta, PNG.
  let blob = await toBlob(canvas, 'image/webp', quality);
  let type = 'image/webp';
  let ext = 'webp';
  if (!blob) {
    blob = await toBlob(canvas, 'image/png', quality);
    type = 'image/png';
    ext = 'png';
  }
  if (!blob) return file;

  // Se la compressione non ha ridotto (immagini già piccole), tieni l'originale.
  if (blob.size >= file.size && file.type !== 'image/svg+xml') return file;

  const name = file.name.replace(/\.[^.]+$/, '') + '.' + ext;
  return new File([blob], name, { type });
}

function loadBitmap(file: File): Promise<ImageBitmap | HTMLImageElement> {
  if ('createImageBitmap' in window) {
    return createImageBitmap(file);
  }
  // Fallback (browser senza createImageBitmap).
  return new Promise((resolve, reject) => {
    const img = new Image();
    const url = URL.createObjectURL(file);
    img.onload = () => {
      URL.revokeObjectURL(url);
      resolve(img);
    };
    img.onerror = (e) => {
      URL.revokeObjectURL(url);
      reject(e);
    };
    img.src = url;
  });
}

function toBlob(canvas: HTMLCanvasElement, type: string, quality: number): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob((b) => resolve(b), type, quality));
}
