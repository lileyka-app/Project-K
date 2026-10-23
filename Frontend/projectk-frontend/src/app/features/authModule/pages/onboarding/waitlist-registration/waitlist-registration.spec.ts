import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MessageService } from '@openng/optimus-ui/api';
import { Observable, of, throwError } from 'rxjs';
import { WaitlistRegistrationComponent } from './waitlist-registration';
import { KraiOption, OnboardingService, WaitlistRegistration } from '../../../services/onboarding-service/onboarding.service';

describe('WaitlistRegistrationComponent', () => {
  let fixture: ComponentFixture<WaitlistRegistrationComponent>;
  let component: WaitlistRegistrationComponent;
  let submitted: WaitlistRegistration | undefined;

  const krais: KraiOption[] = [
    { code: 'ua', name: 'Україна', isDefault: true },
    { code: 'pl', name: 'Польща', isDefault: false }
  ];

  function create(krais$: Observable<KraiOption[]> = of(krais)): void {
    submitted = undefined;
    TestBed.configureTestingModule({
      imports: [WaitlistRegistrationComponent],
      providers: [
        provideRouter([]),
        MessageService,
        {
          provide: OnboardingService,
          useValue: {
            submitWaitlist: (registration: WaitlistRegistration) => {
              submitted = registration;
              return of(undefined);
            },
            getKurinNumberRange: () => of({ min: 1, max: 200 }),
            getKrais: () => krais$
          }
        }
      ]
    });
    fixture = TestBed.createComponent(WaitlistRegistrationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  beforeEach(() => create());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function fillEverythingBut(leader: boolean): void {
    component.form.patchValue({
      firstName: 'Марта', lastName: 'Сова', email: 'marta@example.com', phoneNumber: '+380 50 123 45 67',
      dateOfBirth: new Date(1990, 0, 1), stanytsia: 'Тернопіль',
      isKurinLeaderCandidate: leader, claimedKurinNameOrNumber: '12'
    });
    fixture.detectChanges();
  }

  it('keeps the button disabled and explains itself while the applicant is not a Зв’язковий', () => {
    fillEverythingBut(false);

    expect(component.form.invalid).toBe(true);
    expect(element().querySelector('button[type=submit]')?.hasAttribute('disabled')).toBe(true);
    expect(element().textContent).toContain('приймаємо лише курені');
  });

  it('lets a Зв’язковий with a kurin number apply, and drops the notice', () => {
    fillEverythingBut(true);

    expect(component.form.valid).toBe(true);
    expect(element().querySelector('button[type=submit]')?.hasAttribute('disabled')).toBe(false);
    expect(element().textContent).not.toContain('приймаємо лише курені');
  });

  it('refuses a kurin number outside the range, so nobody takes kurin 0 or 1153', () => {
    fillEverythingBut(true);

    for (const number of ['0', '201', '1153']) {
      component.form.patchValue({ claimedKurinNameOrNumber: number });
      expect(component.form.invalid, number).toBe(true);
    }
    component.form.patchValue({ claimedKurinNameOrNumber: '200' });
    expect(component.form.valid).toBe(true);
  });

  it('comes with the default край picked, and sends the one the applicant chose', () => {
    expect(component.form.value.kraiCode).toBe('ua');
    expect(element().querySelector('#regionOrCountry')).toBeNull();

    fillEverythingBut(true);
    component.form.patchValue({ kraiCode: 'pl' });
    component.onSubmit();

    expect(submitted?.kraiCode).toBe('pl');
    expect(submitted?.regionOrCountry).toBeUndefined();
  });

  it('will not send without a край', () => {
    fillEverythingBut(true);
    component.form.patchValue({ kraiCode: null });

    expect(component.form.invalid).toBe(true);
  });

  it('asks for the край in words when the list does not load', () => {
    TestBed.resetTestingModule();
    create(throwError(() => new Error('offline')));
    fillEverythingBut(true);

    expect(element().querySelector('#regionOrCountry')).not.toBeNull();
    expect(component.form.invalid).toBe(true);

    component.form.patchValue({ regionOrCountry: 'Польща' });
    expect(component.form.valid).toBe(true);
    expect(component.form.value.kraiCode).toBeUndefined();
  });

  it('lets a підготовчий курінь apply without a number', () => {
    fillEverythingBut(true);
    component.form.patchValue({ isPreparatoryKurin: true });
    fixture.detectChanges();

    expect(component.form.valid).toBe(true);
    expect(component.form.value.claimedKurinNameOrNumber).toBeUndefined();
    expect(component.form.value.isPreparatoryKurin).toBe(true);
    expect(element().querySelector('#kurin')).toBeNull();
  });
});
