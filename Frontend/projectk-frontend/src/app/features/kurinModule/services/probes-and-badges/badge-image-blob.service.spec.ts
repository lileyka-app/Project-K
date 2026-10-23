import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { BadgeImageBlobService } from './badge-image-blob.service';
import { environment } from '../../../../../environments/environment';

describe('BadgeImageBlobService', () => {
  let service: BadgeImageBlobService;
  let httpMock: HttpTestingController;
  let apiOrigin: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        BadgeImageBlobService
      ]
    });

    service = TestBed.inject(BadgeImageBlobService);
    httpMock = TestBed.inject(HttpTestingController);
    apiOrigin = resolveApiOrigin();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should return same URL for non-protected images', () => {
    const url = 'https://example.com/image.png';

    const resolved = service.resolveBadgeImageForDisplay(url);

    expect(resolved).toBe(url);
    httpMock.expectNone(url);
  });

  it('should fetch protected image as blob and return object URL from cache', () => {
    const sourceUrl = '/badges_images/skill.png';
    const blob = new Blob(['image-bytes'], { type: 'image/png' });
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob://skill-1');
    const expectedUrl = buildExpectedUrl(apiOrigin, sourceUrl);

    const firstResolve = service.resolveBadgeImageForDisplay(sourceUrl);
    expect(firstResolve).toBeNull();

    const req = httpMock.expectOne(expectedUrl);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(blob);

    const secondResolve = service.resolveBadgeImageForDisplay(sourceUrl);
    expect(secondResolve).toBe('blob://skill-1');
  });

  it('should avoid duplicate requests while image load is pending', () => {
    const sourceUrl = '/badges_images/skill-2.png';
    const expectedUrl = buildExpectedUrl(apiOrigin, sourceUrl);

    const firstResolve = service.resolveBadgeImageForDisplay(sourceUrl);
    const secondResolve = service.resolveBadgeImageForDisplay(sourceUrl);

    expect(firstResolve).toBeNull();
    expect(secondResolve).toBeNull();

    httpMock.expectOne(expectedUrl);
    httpMock.expectNone(expectedUrl);
  });
});

function resolveApiOrigin(): string {
  try {
    return new URL(environment.apiUrl).origin;
  } catch {
    return '';
  }
}

function buildExpectedUrl(apiOrigin: string, imageUrl: string): string {
  if (!apiOrigin) {
    return imageUrl;
  }

  if (imageUrl.startsWith('/badges_images/') || imageUrl.startsWith('/api/awards/images/')) {
    return `${apiOrigin}${imageUrl}`;
  }

  return imageUrl;
}