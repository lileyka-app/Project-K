import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { DuesAccountDto, DuesAccountQuarterDto, PlastYearDto, QuarterDto } from '../../models/group-dues.dto';
import { DuesRegisterComponent } from './dues-register';
import { registerCsv, registerRow } from './dues-register.function';

const q = (year: number, number: number): QuarterDto => ({ year, number });
const amount = (total: number) => ({ stanytsia: 0, krai: 0, kurin: total, group: 0, total });
const quarter = (at: QuarterDto, charged: number, paid: number): DuesAccountQuarterDto => ({
  quarter: at,
  charged: amount(charged),
  paid: amount(paid),
  balance: paid - charged,
  isConcession: false,
  concessionName: null
});

function account(overrides: Partial<DuesAccountDto>): DuesAccountDto {
  return {
    membershipKey: 'm',
    memberKey: 'p',
    fullName: '—',
    standing: 'Current',
    isConcessionNow: false,
    tagKeys: [],
    rateNow: 450,
    grants: [],
    quarters: [],
    charged: 0,
    payments: 0,
    balance: 0,
    ...overrides
  };
}

const year: PlastYearDto = { startYear: 2026, label: '26–27', quarters: [q(2026, 4), q(2027, 1), q(2027, 2), q(2027, 3)] };
const current = q(2026, 4);

// Богдан: two quarters paid and 900 over; Олена: owes the third quarter and the running one.
const ahead = account({
  membershipKey: 'm1', fullName: 'Богдан', tagKeys: ['usp'],
  quarters: [quarter(q(2026, 3), 450, 450), quarter(q(2026, 4), 450, 450)], balance: 900
});
const owing = account({
  membershipKey: 'm2', fullName: 'Олена', tagKeys: ['ups'], rateNow: 350,
  quarters: [quarter(q(2026, 3), 350, 0), quarter(q(2026, 4), 350, 0)], balance: -700
});
const left = account({ membershipKey: 'm3', fullName: 'Вибулий', standing: 'Left', rateNow: null, quarters: [quarter(q(2026, 3), 400, 0)], balance: -400 });

describe('registerRow', () => {
  it('рахує сплачене за рік, борг за минулі квартали й поточний квартал', () => {
    const row = registerRow(owing, year, current);

    expect(row.paidThisYear).toBe(0);
    expect(row.debtPast).toBe(350);
    expect(row.current?.balance).toBe(-350);
    expect(row.paidUntil).toBeNull();
  });

  it('з надлишком «сплачено до» йде вперед на стільки кварталів, скільки покриває ставка', () => {
    expect(registerRow(ahead, year, current).paidUntil).toEqual(q(2027, 2));
    expect(registerRow({ ...ahead, balance: 0 }, year, current).paidUntil).toEqual(q(2026, 4));
  });
});

describe('registerCsv', () => {
  it('не дає таблиці виконати імʼя як формулу, а числа лишає числами', () => {
    const row = registerRow({ ...owing, fullName: '=HYPERLINK("x")' }, year, current);
    const [, line] = registerCsv([row], [], 'Людина', current).split('\r\n');
    expect(line.startsWith('"\'=HYPERLINK(""x"")"')).toBe(true);
    expect(line).toMatch(/;-\d/);
  });

  it('пише рядок на людину з тегами, через крапку з комою', () => {
    const csv = registerCsv([registerRow(owing, year, current)], [{ tagKey: 'ups', name: 'УПС', colorHex: '#2B6CB0' }], 'Людина', current);
    const [header, line] = csv.split('\r\n');

    expect(header).toBe('Людина;Теги;Ставка за квартал;Сплачено за рік;Борг за минулі;IV кв. 2026;Сплачено до;Баланс');
    expect(line).toBe('Олена;УПС;350;0;350;0 / 350;;-700');
  });
});

describe('DuesRegisterComponent', () => {
  let fixture: ComponentFixture<DuesRegisterComponent>;
  let component: DuesRegisterComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [DuesRegisterComponent], providers: [provideNoopAnimations()] });
    fixture = TestBed.createComponent(DuesRegisterComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('accounts', [ahead, owing, left]);
    fixture.componentRef.setInput('year', year);
    fixture.componentRef.setInput('currentQuarter', current);
    fixture.componentRef.setInput('tags', [
      { tagKey: 'usp', name: 'УСП', colorHex: '#2F855A' },
      { tagKey: 'ups', name: 'УПС', colorHex: '#2B6CB0' }
    ]);
    fixture.detectChanges();
  });

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('ділить людей на теперішніх і вибулих з боргом', () => {
    expect(component.currentRows().map(r => r.account.fullName)).toEqual(['Богдан', 'Олена']);
    expect(component.formerRows().map(r => r.account.fullName)).toEqual(['Вибулий']);
    expect(text()).toContain('II кв. 2027');
  });

  it('фільтрує за тегом і за боргом', () => {
    component.filter.set(['usp']);
    expect(component.filtered().map(r => r.account.fullName)).toEqual(['Богдан']);

    component.filter.set(['__debt__']);
    expect(component.filtered().map(r => r.account.fullName)).toEqual(['Олена', 'Вибулий']);
  });

  it('у вигляді «По кварталах» показує клітинку на квартал року', () => {
    component.view.set('quarters');
    fixture.detectChanges();

    const row = component.currentRows()[1];
    expect(component.cellFor(row, q(2026, 4))?.balance).toBe(-350);
    expect(component.cellFor(row, q(2027, 1))).toBeNull();
    expect(text()).toContain('I 2027');
  });
});
