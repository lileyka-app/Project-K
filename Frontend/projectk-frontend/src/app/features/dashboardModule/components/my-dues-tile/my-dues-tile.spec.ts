import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MyDuesDto } from '../../models/me.dto';
import { MyDuesTileComponent } from './my-dues-tile';

function dues(balance: number, kurinNumber = 1): MyDuesDto {
  return {
    kurin: { kurinKey: `k${kurinNumber}`, kurinNumber, namedAfter: null, isCurrent: kurinNumber === 1 },
    groupName: 'Соколи',
    currency: 'UAH',
    numberLocale: 'uk-UA',
    quarterYear: 2026,
    quarterNumber: 4,
    balance,
    quarterRate: 150,
    isConcession: false
  };
}

describe('MyDuesTileComponent', () => {
  let fixture: ComponentFixture<MyDuesTileComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MyDuesTileComponent] }).compileComponents();
    fixture = TestBed.createComponent(MyDuesTileComponent);
  });

  it('says debt in red with the way to settle it, paid in green, a surplus with a plus', () => {
    fixture.componentRef.setInput('dues', [dues(-300, 1), dues(0, 2), dues(50, 3)]);
    fixture.detectChanges();

    const rows = fixture.componentInstance.rows();
    expect(rows.map(r => r.standing)).toEqual(['debt', 'ok', 'surplus']);
    expect(rows[0].amount).toBe('−300 ₴');
    expect(rows[0].note).toContain('скарбникові');
    expect(rows[1].amount).toBe('Сплачено');
    expect(rows[2].amount).toBe('+50 ₴');
    expect(rows[0].quarter).toBe('IV кв. 2026');
    expect(fixture.componentInstance.owing()).toBe(1);

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelectorAll('.dues-tile__row--debt').length).toBe(1);
    expect(el.querySelectorAll('.dues-tile__row--ok').length).toBe(1);
  });
});
