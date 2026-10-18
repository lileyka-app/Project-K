import { registerLocaleData } from '@angular/common';
import localeUk from '@angular/common/locales/uk';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { MessageService } from '@openng/optimus-ui/api';
import { WaitlistManagementComponent } from './waitlist-management';
import { OnboardingService, WaitlistEntry } from '../../../authModule/services/onboarding-service/onboarding.service';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';

function entry(overrides: Partial<WaitlistEntry>): WaitlistEntry {
  return {
    waitlistEntryKey: 'key-1',
    firstName: 'Олена',
    lastName: 'Новак',
    email: 'olena@example.com',
    phoneNumber: '+48 500 000 000',
    dateOfBirth: '1996-03-02',
    stanytsia: 'Варшава',
    isKurinLeaderCandidate: true,
    claimedKurinNameOrNumber: '12',
    verificationStatus: 'Submitted',
    source: 'Application',
    requestedAtUtc: '2026-10-18T10:00:00Z',
    ...overrides
  };
}

describe('WaitlistManagementComponent', () => {
  let fixture: ComponentFixture<WaitlistManagementComponent>;
  let component: WaitlistManagementComponent;
  let onboarding: jasmine.SpyObj<OnboardingService>;

  beforeEach(async () => {
    // The table's date pipe formats in uk-UA, which main.ts registers for the app but not for the specs.
    registerLocaleData(localeUk, 'uk-UA');
    onboarding =jasmine.createSpyObj<OnboardingService>('OnboardingService', ['getWaitlistEntries', 'getKrais', 'approveWaitlistEntry']);
    onboarding.getWaitlistEntries.and.returnValue(of([
      entry({ kraiCode: 'pl' }),
      entry({ waitlistEntryKey: 'key-2', email: 'old@example.com', kraiCode: null, regionOrCountry: 'Польща, Варшава' })
    ]));
    onboarding.getKrais.and.returnValue(of([
      { code: 'ua', name: 'Україна', isDefault: true },
      { code: 'pl', name: 'Польща', isDefault: false }
    ]));
    onboarding.approveWaitlistEntry.and.returnValue(of('invitation-key'));

    await TestBed.configureTestingModule({
      imports: [WaitlistManagementComponent],
      providers: [
        provideNoopAnimations(),
        MessageService,
        { provide: OnboardingService, useValue: onboarding },
        { provide: AuthService, useValue: {} }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WaitlistManagementComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('names the край, or shows what an older application wrote instead', () => {
    expect(component.kraiName(component.entries[0])).toBe('Польща');
    expect(component.kraiName(component.entries[1])).toBe('Польща, Варшава');
  });

  it('offers the applicant’s край on approval and opens the kurin in the one the admin settles on', () => {
    component.approve(component.entries[0]);
    expect(component.approvalKrai).toBe('pl');

    component.approvalKrai = 'ua';
    component.confirmApproval();

    expect(onboarding.approveWaitlistEntry).toHaveBeenCalledWith('key-1', 'ua');
    expect(component.approvalDialogVisible).toBeFalse();
  });

  it('starts from the default край when the application named none', () => {
    component.approve(component.entries[1]);

    expect(component.approvalKrai).toBe('ua');
  });

  it('sends no край for a person who opens no kurin', () => {
    component.approve(entry({ isKurinLeaderCandidate: false, kraiCode: 'pl' }));
    component.confirmApproval();

    expect(onboarding.approveWaitlistEntry).toHaveBeenCalledWith('key-1', null);
  });
});
