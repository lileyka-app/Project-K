import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BehaviorSubject, of } from 'rxjs';
import { DuesEntryKind, DuesPaymentMethod, DuesRecipient, DuesUpstream } from '../../models/dues.enums';
import { DuesEntryDto, DuesTransferDto, KurinDuesDto } from '../../models/group-dues.dto';
import { DuesService } from '../../services/dues-service/dues.service';
import { KurinDuesComponent } from './kurin-dues';

describe('KurinDuesComponent', () => {
  let keys = 0;
  const nextKey = () => String(++keys);

  let fixture: ComponentFixture<KurinDuesComponent>;
  let component: KurinDuesComponent;
  let dues: jasmine.SpyObj<DuesService>;

  const q = (year: number, number: number) => ({ year, number });

  const transfer = (over: Partial<DuesTransferDto>): DuesTransferDto => ({
    duesEntryKey: 't-' + nextKey(),
    groupKey: 'g-sokoly',
    groupName: 'Соколи',
    amount: 255,
    method: DuesPaymentMethod.Cash,
    occurredOn: '2026-10-03',
    collectedByName: null,
    note: null,
    isReceived: false,
    receivedAtUtc: null,
    receivedByName: null,
    ...over
  });

  const entry = (over: Partial<DuesEntryDto>): DuesEntryDto => ({
    duesEntryKey: 'e-' + nextKey(),
    kind: DuesEntryKind.Expense,
    method: DuesPaymentMethod.Cash,
    counterMethod: null,
    amount: 20,
    occurredOn: '2026-10-04',
    membershipKey: null,
    memberName: null,
    collectedByMemberKey: null,
    collectedByName: null,
    note: null,
    isVerified: false,
    verifiedAtUtc: null,
    verifiedByName: null,
    receivedAtUtc: null,
    createdAtUtc: '2026-10-04T10:00:00Z',
    ...over
  });

  const data: KurinDuesDto = {
    kurinKey: 'k1',
    currency: 'UAH',
    numberLocale: 'uk-UA',
    upstream: DuesUpstream.Stanytsia,
    currentQuarter: q(2026, 4),
    years: [{ startYear: 2026, label: '26–27', quarters: [q(2026, 4), q(2027, 1), q(2027, 2), q(2027, 3)] }],
    rates: [{
      fromQuarter: q(2026, 4),
      articles: [
        { name: 'Станиця', recipient: DuesRecipient.Stanytsia, amount: 240, reducedAmount: 180 },
        { name: 'Курінь', recipient: DuesRecipient.Kurin, amount: 15, reducedAmount: 15 }
      ],
      stanytsiaFull: 240,
      stanytsiaReduced: 180,
      kurinShare: 15,
      kurinReduced: 15,
      kraiFull: 0,
      kraiReduced: 0
    }],
    box: { cash: 255, card: 0, total: 255, toForward: 240, own: 15, inTransit: 100 },
    toStanytsia: 0,
    toKrai: 0,
    sentToStanytsia: 0,
    sentToKrai: 0,
    groups: [
      { groupKey: 'g-sokoly', groupName: 'Соколи', owedUp: 510, transferred: 355, received: 255, outstanding: 155, inTransit: 100 },
      { groupKey: 'g-levy', groupName: 'Леви', owedUp: 0, transferred: 0, received: 0, outstanding: 0, inTransit: 0 }
    ],
    quarters: [{
      quarter: q(2026, 4),
      groups: [{ groupKey: 'g-sokoly', groupName: 'Соколи', youthCount: 9, expectedUp: 2295, collectedUp: 255, debtUp: 2040, stanytsiaExpected: 2160, stanytsiaCollected: 240, kraiExpected: 0, kraiCollected: 0 }],
      total: { groupKey: '', groupName: 'Разом', youthCount: 9, expectedUp: 2295, collectedUp: 255, debtUp: 2040, stanytsiaExpected: 2160, stanytsiaCollected: 240, kraiExpected: 0, kraiCollected: 0 }
    }],
    transfers: [
      transfer({ duesEntryKey: 't-1', isReceived: true, receivedByName: 'Скарбник' }),
      transfer({ duesEntryKey: 't-2', amount: 100 }),
      transfer({ duesEntryKey: 't-3', groupKey: 'g-levy', groupName: 'Леви', amount: 30 })
    ],
    entries: [
      entry({ duesEntryKey: 'e-1', kind: DuesEntryKind.TransferToStanytsia, amount: 240, isVerified: true }),
      entry({ duesEntryKey: 'e-2' })
    ],
    people: [{ memberKey: 'p', fullName: 'Оксана Паливода' }],
    viewer: { canKeep: true, canVerify: true, canSetRates: true }
  };

  function create(response: KurinDuesDto = data): void {
    dues = jasmine.createSpyObj<DuesService>('DuesService', [
      'getKurinDues', 'setTransferReceived', 'createKurinEntry', 'updateKurinEntry', 'deleteKurinEntry', 'setKurinEntryVerified', 'setKurinRate'
    ]);
    dues.getKurinDues.and.returnValue(of(response));
    dues.setTransferReceived.and.returnValue(of({}));
    dues.createKurinEntry.and.returnValue(of({}));

    TestBed.configureTestingModule({
      imports: [KurinDuesComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: DuesService, useValue: dues },
        { provide: ActivatedRoute, useValue: { paramMap: new BehaviorSubject(convertToParamMap({ kurinKey: 'k1' })) } }
      ]
    });
    fixture = TestBed.createComponent(KurinDuesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('показує касу куреня, гуртки й те, що в дорозі', () => {
    create();

    expect(text()).toContain('Каса куреня');
    expect(text()).toContain('Соколи');
    expect(component.pendingTransfers().length).toBe(2);
    expect(component.outstandingTotal()).toBe(155);
    expect(text()).toContain('чекають підтвердження: 2');
  });

  it('показує окремо станичні й крайові гроші, щойно курінь має крайову статтю', () => {
    create();
    expect(component.hasKrai()).toBeFalse();
    expect(text()).not.toContain('До передачі в край');

    TestBed.resetTestingModule();
    create({ ...data, rates: [{ ...data.rates[0], kraiFull: 60, kraiReduced: 30 }], toStanytsia: 240, toKrai: 60, sentToKrai: 120 });

    expect(component.hasKrai()).toBeTrue();
    expect(text()).toContain('До передачі в край');
    expect(text()).toContain('передано 120');
    expect(text()).toContain('До краю');
  });

  it('у краї без станиць ховає станицю й пропонує лише передачу в край', () => {
    create({ ...data, upstream: DuesUpstream.Krai, sentToStanytsia: 0, entries: [], rates: [{ ...data.rates[0], stanytsiaFull: 0, stanytsiaReduced: 0, kraiFull: 240, kraiReduced: 180 }] });

    expect(component.hasStanytsia()).toBeFalse();
    expect(component.hasKrai()).toBeTrue();
    expect(text()).not.toContain('До передачі в станицю');
    expect(text()).toContain('До передачі в край');
    expect(component.kurinKinds()).toContain(DuesEntryKind.TransferToKrai);
    expect(component.kurinKinds()).not.toContain(DuesEntryKind.TransferToStanytsia);
  });

  it('клік по гуртку лишає лише його передачі; перемикач — лише ті, що в дорозі', () => {
    create();

    component.toggleGroup(data.groups[0]);
    expect(component.filteredTransfers().map(t => t.duesEntryKey)).toEqual(['t-1', 't-2']);

    component.pendingOnly.set(true);
    expect(component.filteredTransfers().map(t => t.duesEntryKey)).toEqual(['t-2']);

    component.toggleGroup(data.groups[0]);
    expect(component.filteredTransfers().map(t => t.duesEntryKey)).toEqual(['t-2', 't-3']);
  });

  // Підтвердження — це слово скарбника, що гроші дійшли; після нього касу читаємо наново.
  it('підтвердження передачі йде на сервер і перечитує касу', () => {
    create();

    component.setReceived(data.transfers[1], true);

    expect(dues.setTransferReceived).toHaveBeenCalledWith('k1', 't-2', true);
    expect(dues.getKurinDues).toHaveBeenCalledTimes(2);
  });

  it('розбивка по кварталах показує квартали вибраного року, найновіший першим', () => {
    create();

    expect(component.yearQuarters().map(q => q.quarter.number)).toEqual([4]);
    expect(text()).toMatch(/до станиці 2\s160 ₴/);
    expect(text()).toContain('Має передати');
  });

  it('перевірену операцію куреня не дає змінити', () => {
    create();

    expect(component.canEdit(data.entries[0])).toBeFalse();
    expect(component.canEdit(data.entries[1])).toBeTrue();
    expect(component.amountLabel(data.entries[0])).toBe('−240 ₴');
  });

  it('без прав на ведення каси ні кнопок, ні підтвердження немає', () => {
    create({ ...data, viewer: { canKeep: false, canVerify: false, canSetRates: false } });

    expect(text()).not.toContain('Записати операцію');
    expect(text()).not.toContain('Ставки куреня');
    expect(text()).not.toContain('Отримано');
  });
});
