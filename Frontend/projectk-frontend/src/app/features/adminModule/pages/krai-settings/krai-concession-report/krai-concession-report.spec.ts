import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { KraiConcessionReportComponent } from './krai-concession-report';
import { KraiProfileService } from '../../../services/krai-profile-service/krai-profile.service';
import { KraiConcessionReportDto } from '../../../../duesModule/models/concession.dto';

describe('KraiConcessionReportComponent', () => {
  let fixture: ComponentFixture<KraiConcessionReportComponent>;
  let component: KraiConcessionReportComponent;
  let profiles: MockedObject<KraiProfileService>;

  const report: KraiConcessionReportDto = {
    kraiCode: 'cz',
    kraiName: 'Чехія',
    currency: 'CZK',
    numberLocale: 'uk-UA',
    quarter: { year: 2026, number: 4 },
    kurinCount: 2,
    concessions: [{
      key: 'sibling',
      name: 'Брат чи сестра',
      people: 3,
      reduction: { stanytsia: 120, krai: 0, kurin: 30, group: 0, total: 150 },
      kurins: [{ kurinKey: 'k1', number: 12, people: 2, reduction: 100 }, { kurinKey: 'k2', number: null, people: 1, reduction: 50 }]
    }]
  };

  beforeEach(async () => {
    profiles = {
      concessionReport: vi.fn()
    } as MockedObject<KraiProfileService>;
    profiles.concessionReport.mockReturnValue(of(report));

    await TestBed.configureTestingModule({
      imports: [KraiConcessionReportComponent],
      providers: [{ provide: KraiProfileService, useValue: profiles }]
    }).compileComponents();

    fixture = TestBed.createComponent(KraiConcessionReportComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('kraiCode', 'cz');
    fixture.detectChanges();
  });

  it('first asks for the quarter running by the край\'s clock and picks the one the server names', () => {
    fixture.detectChanges();

    expect(profiles.concessionReport).toHaveBeenCalledTimes(1);

    expect(profiles.concessionReport).toHaveBeenCalledWith('cz', null);
    expect(component.quarter()).toBe('2026-4');
    expect(component.quarterOptions()[0].value).toBe('2026-4');
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Брат чи сестра');
    expect(text).toContain('Курінь 12: 2');
    expect(text).toContain('Підготовчий курінь: 1');
    expect(component.totalPeople()).toBe(3);
  });

  it('reads again when another quarter is picked', () => {
    fixture.detectChanges();
    component.quarter.set(component.quarterOptions()[1].value);
    fixture.detectChanges();

    expect(profiles.concessionReport).toHaveBeenCalledWith('cz', { year: 2026, number: 3 });
  });

  it('drops an older answer that arrives after a newer one', () => {
    const older = new Subject<KraiConcessionReportDto>();
    const newer = new Subject<KraiConcessionReportDto>();
    fixture.detectChanges();
    profiles.concessionReport.mockReturnValueOnce(older).mockReturnValueOnce(newer);

    component.quarter.set('2026-3');
    fixture.detectChanges();
    component.quarter.set('2026-2');
    fixture.detectChanges();
    newer.next({ ...report, quarter: { year: 2026, number: 2 }, concessions: [] });
    older.next({ ...report, quarter: { year: 2026, number: 3 } });

    expect(component.quarter()).toBe('2026-2');
    expect(component.report()!.concessions).toEqual([]);
  });
});
