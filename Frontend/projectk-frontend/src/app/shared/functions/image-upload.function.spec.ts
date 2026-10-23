import { MAX_IMAGE_BYTES, isImageTooLarge } from './image-upload.function';

describe('image upload size', () => {
  it('lets through what the API accepts and stops what it would drop', () => {
    expect(isImageTooLarge(new Blob([new Uint8Array(MAX_IMAGE_BYTES)]))).toBe(false);
    expect(isImageTooLarge(new Blob([new Uint8Array(MAX_IMAGE_BYTES + 1)]))).toBe(true);
    expect(isImageTooLarge(null)).toBe(false);
  });
});
