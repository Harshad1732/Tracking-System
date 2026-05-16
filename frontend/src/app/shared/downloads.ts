import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/**
 * Hit an authenticated GET endpoint, receive the response as a blob,
 * and trigger a browser download with the server-suggested filename
 * (or a fallback if the Content-Disposition header is missing).
 */
export async function downloadAuthorized(
  http: HttpClient,
  url: string,
  params: Record<string, string | undefined> = {},
  fallbackName = 'download'
): Promise<void> {
  let p = new HttpParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== null && v !== '') p = p.set(k, v);
  }
  const response = await firstValueFrom(
    http.get(url, { params: p, responseType: 'blob', observe: 'response' })
  );

  const blob = response.body!;
  const cd = response.headers.get('Content-Disposition') ?? '';
  const match = /filename\*?="?([^";]+)"?/i.exec(cd);
  const name = match?.[1] ? decodeURIComponent(match[1]) : fallbackName;

  const objectUrl = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = objectUrl;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(objectUrl);
}
