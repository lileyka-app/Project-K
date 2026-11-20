import { HttpResponse } from '@angular/common/http';

/** Віддає файл людині так, ніби вона клацнула посилання на нього. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = window.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.URL.revokeObjectURL(url);
}

/** Назва, яку дав сервер у `Content-Disposition`, або запасна, коли її нема. */
export function fileNameFrom(response: HttpResponse<Blob>, fallback: string): string {
  const disposition = response.headers.get('content-disposition');
  const match = disposition?.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i);
  return match?.[1] ? decodeURIComponent(match[1].replace(/"$/g, '')) : fallback;
}
