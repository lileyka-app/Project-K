import type { Mock } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { BadgeCatalogItemDto } from '../../models/probes-and-badges/badge-catalog-item.dto';
import { BadgesCatalogService } from '../../services/probes-and-badges/badges-catalog.service';
import { BadgeImageBlobService } from '../../services/probes-and-badges/badge-image-blob.service';
import { BadgeDetailsDialogComponent } from './badge-details-dialog';

const badge: BadgeCatalogItemDto = {
  id: 'plavets-i',
  title: 'Плавець I',
  imagePath: 'https://catalog.test/v1/images/plavets.png',
  country: 'Україна',
  specialization: 'Водна',
  status: 'Затверджена',
  level: 1,
  lastUpdated: '2020-04-29',
  seekerRequirements: '1. Пропливти 100 м\n\nАвтор: Ольга',
  instructorRequirements: 'Інструктор з плавання',
  fixNotes: [],
  requirements: [{ id: '1', text: 'Пропливти 100 м' }],
  author: 'Ольга',
  ulady: ['УПЮ']
};

describe('BadgeDetailsDialogComponent', () => {
  let fixture: ComponentFixture<BadgeDetailsDialogComponent>;
  let getById: Mock;

  beforeEach(async () => {
    getById = vi.fn().mockName('getById').mockReturnValue(of(badge));
    await TestBed.configureTestingModule({
      imports: [BadgeDetailsDialogComponent],
      providers: [
        provideNoopAnimations(),
        { provide: BadgesCatalogService, useValue: { getById } },
        { provide: BadgeImageBlobService, useValue: { resolveBadgeImageForDisplay: (url: string | null) => url } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BadgeDetailsDialogComponent);
  });

  async function open(value: BadgeCatalogItemDto): Promise<HTMLElement> {
    getById.mockReturnValue(of(value));
    fixture.componentRef.setInput('badgeId', value.id);
    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return document.body;
  }

  it('shows the badge like the catalogue card', async () => {
    const page = await open(badge);

    expect(getById).toHaveBeenCalledWith('plavets-i');
    expect(page.querySelector('.badge-details__title')?.textContent).toContain('Плавець I');
    const chips = Array.from(page.querySelectorAll('.badge-details__chip')).map(chip => chip.textContent?.trim());
    expect(chips).toEqual(['Україна', 'рівень 1', 'УПЮ', 'Водна']);
    expect(page.querySelectorAll('.badge-details__requirements li')).toHaveLength(1);
    expect(page.querySelector('.badge-details__footnote')?.textContent).toContain('Розробив: Ольга. Оновлено 29.04.2020.');
  });

  it('renders the catalogue bold marks as bold, without HTML', async () => {
    const page = await open({ ...badge, id: 'bold', requirements: [{ id: '1', text: 'Знати: **а)** грозу <b>і</b> **б)** град **' }] });

    const item = page.querySelector('.badge-details__requirements li');
    expect(Array.from(item?.querySelectorAll('strong') ?? []).map(strong => strong.textContent)).toEqual(['а)', 'б)']);
    expect(item?.textContent).toContain('<b>і</b>');
    expect(item?.textContent?.trim().endsWith('**')).toBe(true);
  });

  it('falls back to the requirements text when the API sends no list', async () => {
    const page = await open({ ...badge, id: 'old', requirements: undefined, author: undefined, status: 'Апробаційна' });

    expect(page.querySelector('.badge-details__requirements')).toBeNull();
    expect(page.querySelector('.badge-details__text')?.textContent).toContain('Пропливти 100 м');
    expect(Array.from(page.querySelectorAll('.badge-details__chip')).map(chip => chip.textContent?.trim())).toContain('апробаційна');
  });
});
