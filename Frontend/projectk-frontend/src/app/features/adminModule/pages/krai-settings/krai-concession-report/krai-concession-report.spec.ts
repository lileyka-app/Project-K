import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { KraiConcessionReportComponent } from './krai-concession-report';
import { KraiProfileService } from '../../../services/krai-profile-service/krai-profile.service';
import { KraiConcessionReportDto } from '../../../../duesModule/models/concession.dto';

describe('KraiConcessionReportComponent', () => {
  let fixture: ComponentFixture<KraiConcessionReportComponent>;
  let component: KraiConcessionReportComponent;
  let profiles: jasmine.SpyObj<KraiProfileService>;

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
    profiles = jasmine.createSpyObj<KraiProfileService>('KraiProfileService', ['concessionReport']);
    profiles.concessionReport.and.returnValue(of(report));

    await TestBed.configureTestingModule({
      imports: [KraiConcessionReportComponent],
      providers: [{ provide: KraiProfileService, useValue: profiles }]
    }).compileComponents();

    fixture = TestBed.createComponent(KraiConcessionReportComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('kraiCode', 'cz');
    fixture.detectChanges();
  });

  it('reads the current quarter for the край and shows each пільга with its kurins', () => {
    expect(profiles.concessionReport).toHaveBeenCalledWith('cz', component.quarterOptions[0].quarter);
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Брат чи сестра');
    expect(text).toContain('Курінь 12: 2');
    expect(text).toContain('Підготовчий курінь: 1');
    expect(component.totalPeople()).toBe(3);
  });

  it('reads again when another quarter is picked', () => {
    component.quarter.set(component.quarterOptions[1].value);
    fixture.detectChanges();

    expect(profiles.concessionReport).toHaveBeenCalledWith('cz', component.quarterOptions[1].quarter);
  });
});
