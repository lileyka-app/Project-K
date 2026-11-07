import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CardService, PublicCardDto } from '../../services/card-service/card.service';
import { PublicCardComponent } from './public-card';
import { KurinBranch } from '../../../kurinModule/models/enums/kurin-branch.enum';

describe('PublicCardComponent', () => {
  let cards: MockedObject<CardService>;
  const card: PublicCardDto = {
    fullName: 'Назар Коваль', photoUrl: null, kurins: [{ number: 15, namedAfter: null, groupName: 'Соколи', branches: [{ branch: KurinBranch.UPYu, number: 15 }] }],
    level: 'пл. уч.', awards: [{ level: 4, title: '4-е відзначення', imageUrl: null, dateAcquired: '2026-05-01T00:00:00Z' }], badges: null, probes: [],
    checkedAtUtc: '2026-10-31T12:32:00Z'
  };

  function create(mode: 'check' | 'link') {
    TestBed.configureTestingModule({
      imports: [PublicCardComponent],
      providers: [
        provideRouter([]),
        { provide: CardService, useValue: cards },
        { provide: ActivatedRoute, useValue: { snapshot: { data: { mode }, paramMap: convertToParamMap({ token: 'tok' }) } } }
      ]
    });
    const fixture = TestBed.createComponent(PublicCardComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    cards = { checked: vi.fn(), linked: vi.fn() } as MockedObject<CardService>;
  });

  it('shows a checked card with the parts it carries and when it was checked', () => {
    cards.checked.mockReturnValue(of(card));
    const text = (create('check').nativeElement as HTMLElement).textContent ?? '';

    expect(cards.checked).toHaveBeenCalledWith('tok');
    expect(text).toContain('Назар Коваль');
    expect(text).toContain('Четверте відзначення');
    expect(text).toContain('Перевірено Лілейкою');
    expect(text).not.toContain('Вмілості');
  });

  it('names a senior kurin on the card by its гілка', () => {
    cards.checked.mockReturnValue(of({
      ...card,
      kurins: [{ number: 5, namedAfter: null, groupName: null, branches: [{ branch: KurinBranch.USP, number: 5 }] }]
    }));
    const text = (create('check').nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('5-й курінь УСП');
    expect(text).not.toContain('к. ч. 5');
  });

  it('shows a senior відзначення with the catalogue title and picture', () => {
    cards.checked.mockReturnValue(of({
      ...card,
      awards: [{ level: null, title: 'Грамота заслуги', imageUrl: 'https://catalog.test/gramota.png', dateAcquired: '2026-05-01T00:00:00Z' }]
    }));
    const element = create('check').nativeElement as HTMLElement;

    expect(element.textContent).toContain('Грамота заслуги');
    expect(element.querySelector('.shared-card__awards img')?.getAttribute('src')).toBe('https://catalog.test/gramota.png');
  });

  it('reads a permanent link from its own address and says when it is gone', () => {
    cards.linked.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    const text = (create('link').nativeElement as HTMLElement).textContent ?? '';

    expect(cards.linked).toHaveBeenCalledWith('tok');
    expect(text).toContain('Картку не знайдено');
    expect(text).toContain('Посилання вимкнене.');
  });
});
