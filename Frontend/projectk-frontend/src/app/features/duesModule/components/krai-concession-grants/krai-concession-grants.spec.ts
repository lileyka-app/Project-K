import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { KraiConcessionGrantsComponent } from './krai-concession-grants';
import { DuesService } from '../../services/dues-service/dues.service';
import { DuesConcessionGrantDto, KraiConcessionDto } from '../../models/concession.dto';
import { fromQuarterOptions } from '../../functions/from-quarter-options.function';

describe('KraiConcessionGrantsComponent', () => {
  let fixture: ComponentFixture<KraiConcessionGrantsComponent>;
  let component: KraiConcessionGrantsComponent;
  let dues: jasmine.SpyObj<DuesService>;
  let confirmation: ConfirmationService;

  const sibling: KraiConcessionDto = { key: 'sibling', name: 'Брат чи сестра', kind: 'percent', value: 20, appliesTo: ['stanytsia'] };
  const grant: DuesConcessionGrantDto = {
    ...sibling,
    grantKey: 'g1',
    concessionKey: 'sibling',
    fromQuarter: { year: 2026, number: 1 },
    toQuarter: null,
    reason: 'брат у курені',
    isActiveNow: true
  };

  beforeEach(async () => {
    dues = jasmine.createSpyObj<DuesService>('DuesService', ['grantConcession', 'updateConcessionGrant', 'deleteConcessionGrant']);
    dues.grantConcession.and.returnValue(of({}));
    dues.updateConcessionGrant.and.returnValue(of({}));
    dues.deleteConcessionGrant.and.returnValue(of({}));

    await TestBed.configureTestingModule({
      imports: [KraiConcessionGrantsComponent],
      providers: [ConfirmationService, { provide: DuesService, useValue: dues }]
    }).compileComponents();

    fixture = TestBed.createComponent(KraiConcessionGrantsComponent);
    component = fixture.componentInstance;
    confirmation = TestBed.inject(ConfirmationService);
    const current = { year: 2026, number: 2 };
    fixture.componentRef.setInput('groupKey', 'grp');
    fixture.componentRef.setInput('membershipKey', 'm1');
    fixture.componentRef.setInput('grants', [grant]);
    fixture.componentRef.setInput('concessions', [sibling]);
    fixture.componentRef.setInput('fromOptions', fromQuarterOptions([{ startYear: 2025, label: '2025/26', quarters: [{ year: 2026, number: 1 }, current] }], current));
    fixture.componentRef.setInput('currentQuarter', current);
    fixture.componentRef.setInput('money', (amount: number) => `${amount} Kč`);
    fixture.detectChanges();
  });

  it('shows what is given, with its rule, its quarters and why', () => {
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Брат чи сестра');
    expect(text).toContain('−20 % · станиця');
    expect(text).toContain('з I кв. 2026, без кінця');
    expect(text).toContain('брат у курені');
  });

  it('gives a пільга from the current quarter until the end picked', () => {
    const changed = jasmine.createSpy('changed');
    component.changed.subscribe(changed);

    component.concessionKey.set('sibling');
    component.startGranting();
    component.toQuarter.set('2026-4');
    component.reason.set('  двоє дітей  ');
    component.grant();

    expect(dues.grantConcession).toHaveBeenCalledWith('grp', 'm1', {
      concessionKey: 'sibling',
      fromQuarter: { year: 2026, number: 2 },
      toQuarter: { year: 2026, number: 4 },
      reason: 'двоє дітей'
    });
    expect(changed).toHaveBeenCalled();
    expect(component.concessionKey()).toBeNull();
  });

  it('ends a given пільга', () => {
    component.edit(grant);
    component.editTo.set('2026-3');
    component.saveEdit(grant);

    expect(dues.updateConcessionGrant).toHaveBeenCalledWith('grp', 'g1', { toQuarter: { year: 2026, number: 3 }, reason: 'брат у курені' });
    expect(component.editingKey()).toBeNull();
  });

  it('deletes only after it is confirmed', () => {
    spyOn(confirmation, 'confirm').and.callFake(options => { options.accept?.(); return confirmation; });

    component.remove(grant);

    expect(dues.deleteConcessionGrant).toHaveBeenCalledWith('grp', 'g1');
  });

  it('shows why the server refused', () => {
    dues.grantConcession.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { message: 'Ця пільга вже діє в цих кварталах.' }
    })));

    component.concessionKey.set('sibling');
    component.startGranting();
    component.grant();

    expect(component.error()).toBe('Ця пільга вже діє в цих кварталах.');
  });
});
