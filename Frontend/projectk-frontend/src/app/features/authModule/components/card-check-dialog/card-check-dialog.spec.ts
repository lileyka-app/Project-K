import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { CardService } from '../../services/card-service/card.service';
import { CardCheckDialogComponent } from './card-check-dialog';

describe('CardCheckDialogComponent', () => {
  let cards: MockedObject<CardService>;
  const code = (secondsLeft: number) => ({
    url: 'https://lileyka.test/v/t', qrCode: 'data:image/png;base64,AA', expiresAtUtc: new Date(Date.now() + secondsLeft * 1000).toISOString()
  });

  function create() {
    TestBed.configureTestingModule({
      imports: [CardCheckDialogComponent],
      providers: [provideNoopAnimations(), provideRouter([]), { provide: CardService, useValue: cards }]
    });
    const fixture = TestBed.createComponent(CardCheckDialogComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    vi.useFakeTimers();
    cards = { checkCode: vi.fn() } as MockedObject<CardService>;
  });

  afterEach(() => vi.useRealTimers());

  it('asks for a code only while open, and a fresh one before the old expires', () => {
    cards.checkCode.mockReturnValue(of(code(30)));
    const fixture = create();
    expect(cards.checkCode).not.toHaveBeenCalled();

    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();
    expect(cards.checkCode).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance.secondsLeft()).toBe(30);

    vi.advanceTimersByTime(11_000);
    expect(cards.checkCode).toHaveBeenCalledTimes(2);

    fixture.componentRef.setInput('visible', false);
    fixture.detectChanges();
    vi.advanceTimersByTime(60_000);
    expect(cards.checkCode).toHaveBeenCalledTimes(2);
  });

  it('says the kurin has not allowed it when the server refuses', () => {
    cards.checkCode.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    const fixture = create();

    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();

    expect(fixture.componentInstance.notAllowed()).toBe(true);
    expect((fixture.nativeElement as HTMLElement).ownerDocument.body.textContent).toContain('Курінь ще не дозволив');
  });
});
