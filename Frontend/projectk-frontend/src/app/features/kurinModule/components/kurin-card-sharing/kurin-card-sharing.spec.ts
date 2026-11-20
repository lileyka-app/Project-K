import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CardService } from '../../../authModule/services/card-service/card.service';
import { KurinCardSharingComponent } from './kurin-card-sharing';

describe('KurinCardSharingComponent', () => {
  let cards: MockedObject<CardService>;

  function create() {
    TestBed.configureTestingModule({
      imports: [KurinCardSharingComponent],
      providers: [{ provide: CardService, useValue: cards }]
    });
    const fixture = TestBed.createComponent(KurinCardSharingComponent);
    fixture.componentRef.setInput('kurinKey', 'k1');
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    cards = { kurinSharing: vi.fn(), setKurinSharing: vi.fn() } as MockedObject<CardService>;
    cards.kurinSharing.mockReturnValue(of({ minorsMayShare: false, minorFields: 6, minorsMayLink: false }));
    cards.setKurinSharing.mockImplementation((_, sharing) => of(sharing));
  });

  it('saves nothing when nothing changed', () => {
    const fixture = create();

    fixture.componentInstance.save().subscribe();

    expect(cards.setKurinSharing).not.toHaveBeenCalled();
  });

  it('allows minors with the chosen parts, and switching sharing off takes the link with it', () => {
    const fixture = create();
    const tile = fixture.componentInstance as unknown as {
      setShare(on: boolean): void; setLink(on: boolean): void; setField(name: string, on: boolean): void;
    };

    tile.setShare(true);
    tile.setLink(true);
    tile.setField('Awards', true);
    fixture.componentInstance.save().subscribe();
    expect(cards.setKurinSharing).toHaveBeenLastCalledWith('k1', { minorsMayShare: true, minorFields: 14, minorsMayLink: true });

    tile.setShare(false);
    fixture.componentInstance.save().subscribe();
    expect(cards.setKurinSharing).toHaveBeenLastCalledWith('k1', { minorsMayShare: false, minorFields: 14, minorsMayLink: false });
  });
});
