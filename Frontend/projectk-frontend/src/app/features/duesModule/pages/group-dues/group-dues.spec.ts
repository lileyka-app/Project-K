import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { DuesEntryKind, DuesPaymentMethod, DuesRecipient, DuesUpstream } from '../../models/dues.enums';
import { DuesAccountDto, DuesEntryDto, GroupDuesDto } from '../../models/group-dues.dto';
import { DuesService } from '../../services/dues-service/dues.service';
import { GroupDuesComponent } from './group-dues';

describe('GroupDuesComponent', () => {
  let keys = 0;
  const nextKey = () => String(++keys);

  let fixture: ComponentFixture<GroupDuesComponent>;
  let component: GroupDuesComponent;
  let dues: jasmine.SpyObj<DuesService>;

  const q = (year: number, number: number) => ({ year, number });
  const amount = (stanytsia: number, kurin: number, group: number) => ({ stanytsia, krai: 0, kurin, group, total: stanytsia + kurin + group });

  const account = (over: Partial<DuesAccountDto>): DuesAccountDto => ({
    membershipKey: 'm-' + nextKey(),
    memberKey: 'p',
    fullName: 'Юнак',
    standing: 'Current',
    isConcessionNow: false,
    quarters: [],
    charged: 0,
    payments: 0,
    balance: 0,
    ...over
  });

  const entry = (over: Partial<DuesEntryDto>): DuesEntryDto => ({
    duesEntryKey: 'e-' + nextKey(),
    kind: DuesEntryKind.Contribution,
    method: DuesPaymentMethod.Cash,
    counterMethod: null,
    amount: 300,
    occurredOn: '2026-05-01',
    membershipKey: null,
    memberName: null,
    collectedByMemberKey: null,
    collectedByName: null,
    note: null,
    isVerified: false,
    verifiedAtUtc: null,
    verifiedByName: null,
    receivedAtUtc: null,
    createdAtUtc: '2026-05-01T10:00:00Z',
    ...over
  });

  const paidUp = account({
    membershipKey: 'm-ok',
    fullName: 'Оксана Паливода',
    quarters: [
      { quarter: q(2025, 4), charged: amount(240, 15, 45), paid: amount(240, 15, 45), balance: 0, isConcession: false },
      { quarter: q(2026, 1), charged: amount(240, 15, 45), paid: amount(240, 15, 45), balance: 0, isConcession: false }
    ],
    charged: 600,
    payments: 600,
    balance: 0
  });

  const debtor = account({
    membershipKey: 'm-debt',
    fullName: 'Тарас Борг',
    quarters: [{ quarter: q(2026, 1), charged: amount(240, 15, 45), paid: amount(150, 0, 0), balance: -150, isConcession: false }],
    charged: 300,
    payments: 150,
    balance: -150
  });

  const moved = account({ membershipKey: 'm-moved', fullName: 'Переведений', standing: 'Moved', balance: -300 });

  const data: GroupDuesDto = {
    groupKey: 'g1',
    kurinKey: 'k1',
    groupName: 'Соколи',
    currency: 'UAH',
    numberLocale: 'uk-UA',
    upstream: DuesUpstream.Stanytsia,
    currentQuarter: q(2026, 2),
    years: [
      { startYear: 2025, label: '25–26', quarters: [q(2025, 4), q(2026, 1), q(2026, 2), q(2026, 3)] },
      { startYear: 2024, label: '24–25', quarters: [q(2024, 4), q(2025, 1), q(2025, 2), q(2025, 3)] }
    ],
    kurinRates: [{
      fromQuarter: q(2025, 4),
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
    groupRates: [{ fromQuarter: q(2025, 4), groupShare: 45 }],
    accounts: [paidUp, debtor, moved],
    entries: [
      entry({ duesEntryKey: 'e-1', membershipKey: 'm-ok', memberName: 'Оксана Паливода', amount: 300, isVerified: true, verifiedByName: 'Впорядник' }),
      entry({ duesEntryKey: 'e-2', membershipKey: 'm-debt', memberName: 'Тарас Борг', amount: 150, occurredOn: '2026-02-10' }),
      entry({ duesEntryKey: 'e-3', kind: DuesEntryKind.Expense, amount: 20, occurredOn: '2025-11-03' })
    ],
    box: { cash: 430, card: 0, total: 430, toForward: 405, own: 25, inTransit: 0 },
    handover: { owedUp: 405, transferred: 0, received: 0, outstanding: 405 },
    people: [{ memberKey: 'p', fullName: 'Оксана Паливода' }],
    viewer: { canKeep: true, canVerify: true, canSetKurinRates: true }
  };

  function create(response: GroupDuesDto = data): void {
    dues = jasmine.createSpyObj<DuesService>('DuesService', [
      'getGroupDues', 'createEntry', 'updateEntry', 'deleteEntry', 'setEntryVerified', 'setGroupRate', 'setConcession'
    ]);
    dues.getGroupDues.and.returnValue(of(response));
    dues.createEntry.and.returnValue(of({}));
    dues.updateEntry.and.returnValue(of({}));
    dues.setEntryVerified.and.returnValue(of({}));

    TestBed.configureTestingModule({
      imports: [GroupDuesComponent],
      providers: [
        provideNoopAnimations(),
        { provide: DuesService, useValue: dues },
        { provide: ActivatedRoute, useValue: { paramMap: new BehaviorSubject(convertToParamMap({ groupKey: 'g1' })) } }
      ]
    });
    fixture = TestBed.createComponent(GroupDuesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('показує квартали поточного пластового року й ділить людей на теперішніх і колишніх', () => {
    create();

    expect(component.year()?.label).toBe('25–26');
    expect(component.currentRows().map(r => r.account.fullName)).toEqual(['Оксана Паливода', 'Тарас Борг']);
    expect(component.formerRows().map(r => r.account.fullName)).toEqual(['Переведений']);
    // Перший квартал року — IV 2025: сплачений у Оксани, ще не нарахований у Тараса.
    expect(component.currentRows()[0].cells[0]?.balance).toBe(0);
    expect(component.currentRows()[1].cells[0]).toBeNull();
    expect(component.debtors()).toBe(2);
  });

  it('рахує квартальну вкладку зі ставок, що діють зараз', () => {
    create();

    expect(component.quarterTotal()).toBe(300);
    expect(text()).toContain('300 ₴');
    expect(text()).toContain('25 ₴');
  });

  it('показує пільгу курінної частини, коли курінь її задав', () => {
    create({ ...data, kurinRates: [{ ...data.kurinRates[0], kurinReduced: 10 }] });

    const rates = (fixture.nativeElement as HTMLElement).querySelector('.dues-rates')?.textContent ?? '';
    expect(rates).toContain('з пільгою 180 ₴');
    expect(rates).toContain('з пільгою 10 ₴');
  });

  it('не показує пільги курінної частини, коли вона дорівнює повній', () => {
    create();

    const rates = (fixture.nativeElement as HTMLElement).querySelector('.dues-rates')?.textContent ?? '';
    expect(rates).not.toContain('з пільгою 15 ₴');
  });

  it('клік по юнаку лишає в історії тільки його операції, другий клік знімає фільтр', () => {
    create();

    component.togglePerson(debtor);
    expect(component.filteredEntries().map(e => e.duesEntryKey)).toEqual(['e-2']);
    expect(component.personFilterName()).toBe('Тарас Борг');

    component.togglePerson(debtor);
    expect(component.filteredEntries().length).toBe(3);
  });

  it('фільтр за кварталом дивиться на дату операції', () => {
    create();

    component.quarterFilter.set('2025-4');
    expect(component.filteredEntries().map(e => e.duesEntryKey)).toEqual(['e-3']);

    component.kindFilter.set(DuesEntryKind.Contribution);
    expect(component.filteredEntries().length).toBe(0);
  });

  // Перевірену операцію впорядник замкнув — правити й видаляти її не можна, і кнопок немає.
  it('перевірену операцію не дає ні змінити, ні видалити', () => {
    create();

    expect(component.canEdit(data.entries[0])).toBeFalse();
    expect(component.canEdit(data.entries[1])).toBeTrue();
  });

  it('витрата й повернення показуються зі знаком мінус, внесок — з плюсом', () => {
    create();

    expect(component.amountLabel(data.entries[1])).toBe('+150 ₴');
    expect(component.amountLabel(data.entries[2])).toBe('−20 ₴');
    expect(component.isIncoming(entry({ kind: DuesEntryKind.Exchange }))).toBeNull();
  });

  it('після запису операції касу читає наново', () => {
    create();
    component.openEntryDialog();

    component.saveEntry({
      kind: DuesEntryKind.Contribution, method: DuesPaymentMethod.Cash, counterMethod: null,
      amount: 300, occurredOn: '2026-05-10', membershipKey: 'm-debt', collectedByMemberKey: null, note: null
    });

    expect(dues.createEntry).toHaveBeenCalledWith('g1', jasmine.objectContaining({ amount: 300 }));
    expect(dues.getGroupDues).toHaveBeenCalledTimes(2);
    expect(component.entryDialogVisible()).toBeFalse();
  });

  it('невдалий запис лишає діалог відкритим із поясненням', () => {
    create();
    dues.createEntry.and.returnValue(throwError(() => ({ status: 409 })));
    component.openEntryDialog();

    component.saveEntry({
      kind: DuesEntryKind.Expense, method: DuesPaymentMethod.Cash, counterMethod: null,
      amount: 10, occurredOn: '2026-05-10', membershipKey: null, collectedByMemberKey: null, note: null
    });

    expect(component.entryDialogVisible()).toBeTrue();
    expect(component.entryError()).toBeTruthy();
  });

  it('без прав на ведення каси кнопок дій немає', () => {
    create({ ...data, viewer: { canKeep: false, canVerify: false, canSetKurinRates: false } });

    expect(text()).not.toContain('Записати операцію');
    expect(text()).not.toContain('Ставка гуртка');
    expect(component.canEdit(data.entries[1])).toBeFalse();
  });

  it('коли касу не вдалося прочитати, пропонує оновити', () => {
    dues = jasmine.createSpyObj<DuesService>('DuesService', ['getGroupDues']);
    dues.getGroupDues.and.returnValue(throwError(() => new Error('down')));
    TestBed.configureTestingModule({
      imports: [GroupDuesComponent],
      providers: [
        provideNoopAnimations(),
        ConfirmationService,
        { provide: DuesService, useValue: dues },
        { provide: ActivatedRoute, useValue: { paramMap: new BehaviorSubject(convertToParamMap({ groupKey: 'g1' })) } }
      ]
    });
    fixture = TestBed.createComponent(GroupDuesComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.loadFailed()).toBeTrue();
    expect(text()).toContain('Оновити');
  });
});
