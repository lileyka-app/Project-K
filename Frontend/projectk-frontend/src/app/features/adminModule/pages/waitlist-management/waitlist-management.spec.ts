import type { MockedObject } from 'vitest';
import { registerLocaleData } from '@angular/common';
import localeUk from '@angular/common/locales/uk';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { MessageService } from '@openng/optimus-ui/api';
import { WaitlistManagementComponent } from './waitlist-management';
import { OnboardingService, WaitlistEntry } from '../../../authModule/services/onboarding-service/onboarding.service';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinBranchRequestService } from '../../../kurinModule/services/kurin-branch-request-service/kurin-branch-request.service';
import { KurinBranchRequestDto } from '../../../kurinModule/models/kurin-branch-request.dto';
import { KurinBranch } from '../../../kurinModule/models/enums/kurin-branch.enum';

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

function branchRequest(overrides: Partial<KurinBranchRequestDto>): KurinBranchRequestDto {
  return {
    kurinBranchRequestKey: 'req-1',
    kurinKey: 'kurin-1',
    kraiCode: 'ua',
    kurinNamedAfter: 'Вітролови',
    currentBranches: [{ branch: KurinBranch.USP, number: 7 }],
    wantedBranches: [{ branch: KurinBranch.USP, number: 7 }, { branch: KurinBranch.UPS, number: 21 }],
    note: null,
    status: 'Pending',
    requestedByName: 'Тарас',
    requestedAtUtc: '2026-10-19T09:00:00Z',
    reviewedAtUtc: null,
    ...overrides
  };
}

describe('WaitlistManagementComponent', () => {
  let fixture: ComponentFixture<WaitlistManagementComponent>;
  let component: WaitlistManagementComponent;
  let onboarding: MockedObject<OnboardingService>;

  beforeEach(async () => {
    // The table's date pipe formats in uk-UA, which main.ts registers for the app but not for the specs.
    registerLocaleData(localeUk, 'uk-UA');
    onboarding = {
      getWaitlistEntries: vi.fn(),
      getKrais: vi.fn(),
      approveWaitlistEntry: vi.fn()
    } as MockedObject<OnboardingService>;
    onboarding.getWaitlistEntries.mockReturnValue(of([
      entry({ kraiCode: 'pl' }),
      entry({ waitlistEntryKey: 'key-2', email: 'old@example.com', kraiCode: null, regionOrCountry: 'Польща, Варшава' })
    ]));
    onboarding.getKrais.mockReturnValue(of([
      { code: 'ua', name: 'Україна', isDefault: true },
      { code: 'pl', name: 'Польща', isDefault: false }
    ]));
    onboarding.approveWaitlistEntry.mockReturnValue(of({ invitationKey: 'invitation-key', invitationSent: true }));

    await TestBed.configureTestingModule({
      imports: [WaitlistManagementComponent],
      providers: [
        provideNoopAnimations(),
        MessageService,
        { provide: OnboardingService, useValue: onboarding },
        { provide: AuthService, useValue: {} },
        { provide: KurinBranchRequestService, useValue: { getAll: () => of([
          branchRequest({}),
          branchRequest({ kurinBranchRequestKey: 'req-2', status: 'Approved', requestedAtUtc: '2026-09-01T09:00:00Z' })
        ]) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WaitlistManagementComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('names a claimed УПЮ kurin with its улад, as a senior claim names its own', () => {
    expect(component.youthClaim({ claimedKurinNameOrNumber: '5' } as never)).toBe('Курінь ч. 5 УПЮ');
    expect(component.youthClaim({ claimedKurinNameOrNumber: 'Лісові' } as never)).toBe('Курінь Лісові УПЮ');
    expect(component.seniorClaim({ claimedUspNumber: 5 } as never)).toBe('5-й курінь УСП');
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
    expect(component.approvalDialogVisible).toBe(false);
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

  it('words an unknown край by its code, not by the server message', () => {
    const add = vi.spyOn(TestBed.inject(MessageService), 'add');
    onboarding.approveWaitlistEntry.mockReturnValue(throwError(() => new HttpErrorResponse({
      status: 400,
      error: { error: 'KraiNotFound', message: 'There is no such krai.' }
    })));

    component.approve(component.entries[0]);
    component.confirmApproval();

    expect(add).toHaveBeenCalledWith(expect.objectContaining({ detail: 'Такого краю немає в списку.' }));
  });

  it('lists what waits, applications and kurin requests together, newest first', () => {
    expect(component.visibleItems.map(item => item.key)).toEqual(['req-1', 'key-1', 'key-2']);
    expect(component.stateOptions[0].label).toBe('Чекають · 3');
  });

  it('narrows the list to one kind, settled ones included', () => {
    component.setState('all');
    component.setKind('branch');

    expect(component.visibleItems.map(item => item.key)).toEqual(['req-1', 'req-2']);
    // A settled kurin already has the wanted улади, so its row names only them.
    expect(component.visibleItems[1].from).toBeNull();
    expect(component.visibleItems[0].from).toBe('7-й курінь УСП');
  });

  it('says so when the approval stood but the letter did not go', () => {
    const add = vi.spyOn(TestBed.inject(MessageService), 'add');
    onboarding.approveWaitlistEntry.mockReturnValue(of({ invitationKey: 'k', invitationSent: false }));

    component.approve(component.entries[0]);
    component.confirmApproval();

    expect(add).toHaveBeenCalledWith(expect.objectContaining({ severity: 'warn' }));
  });
});
