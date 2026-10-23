import { fitWithin } from './account.labels';

/** What the upload endpoint takes as is (ImageUploadRules: PNG, JPEG, WebP up to 5 MB). */
const ACCEPTED = new Set(['image/png', 'image/jpeg', 'image/webp']);
/** A phone photo is 4000+ px and several MB; a screenshot reads fine at this size. */
const MAX_SIDE = 1920;
const MAX_UNTOUCHED_BYTES = 1.5 * 1024 * 1024;

/**
 * A picture from the photo library made ready for upload: small screenshots go as they are; large
 * photos, and formats the server refuses (HEIC), are drawn down to 1920 px and sent as JPEG.
 */
export async function prepareScreenshot(file: File): Promise<{ blob: Blob; name: string }> {
  if (ACCEPTED.has(file.type) && file.size <= MAX_UNTOUCHED_BYTES) {
    const bitmap = await decode(file).catch(() => null);
    const small = !bitmap || Math.max(bitmap.width, bitmap.height) <= MAX_SIDE;
    bitmap?.close?.();
    if (small) return { blob: file, name: file.name || 'screenshot.png' };
  }
  const bitmap = await decode(file);
  const size = fitWithin(bitmap.width, bitmap.height, MAX_SIDE);
  const canvas = document.createElement('canvas');
  canvas.width = size.width;
  canvas.height = size.height;
  const context = canvas.getContext('2d');
  if (!context) throw new Error('No 2D canvas');
  context.drawImage(bitmap, 0, 0, size.width, size.height);
  bitmap.close?.();
  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.85));
  if (!blob) throw new Error('The picture could not be encoded');
  const base = (file.name || 'screenshot').replace(/\.[^.]+$/, '');
  return { blob, name: `${base}.jpg` };
}

type Decoded = { width: number; height: number; close?: () => void } & CanvasImageSource;

/** ImageBitmap where there is one (it honours the photo's orientation), else an <img>. */
async function decode(file: Blob): Promise<Decoded> {
  if (typeof createImageBitmap === 'function') {
    try {
      return await createImageBitmap(file, { imageOrientation: 'from-image' });
    } catch {
      // Fall through to <img>, which some browsers decode more formats with.
    }
  }
  const url = URL.createObjectURL(file);
  try {
    const image = new Image();
    image.src = url;
    await image.decode();
    return Object.assign(image, { width: image.naturalWidth, height: image.naturalHeight });
  } finally {
    URL.revokeObjectURL(url);
  }
}
