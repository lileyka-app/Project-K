import { Injectable, inject } from '@angular/core';
import { LoginResponse } from '../../models/login-response.model';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { requestFeedback } from '../../../../shared/functions/request-feedback.function';

export interface WaitlistRegistration {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  dateOfBirth: string;
  stanytsia: string;
  /** Free text the form sent before it offered the list of краї; now only when the list would not load. */
  regionOrCountry?: string | null;
  /** The край picked from the list; the kurin opens in it once approved. */
  kraiCode?: string | null;
  isKurinLeaderCandidate: boolean;
  claimedKurinNameOrNumber?: string | null;
  /** The applicant leads a підготовчий курінь, which has no number yet. */
  isPreparatoryKurin?: boolean;
}

/** How an entry came to be: a зв'язковий applied, or a провід added the person straight into a kurin. */
export type WaitlistEntrySource = 'Application' | 'DirectAdd';

/** The numbers a kurin may carry. */
export interface KurinNumberRange {
  min: number;
  max: number;
}

/** A край as the join form offers it. */
export interface KraiOption {
  code: string;
  name: string;
  /** The край a kurin opens in when nobody picks one. */
  isDefault: boolean;
}

const kraiNameOrder = new Intl.Collator('uk');

export interface WaitlistEntry extends WaitlistRegistration {
  waitlistEntryKey: string;
  verificationStatus: string | number;
  source?: WaitlistEntrySource;
  requestedAtUtc: string;
  approvedAtUtc?: string;
  invitationSentAtUtc?: string;
  onboardingStatus?: string | number;
}

export interface InvitationValidationResponse {
  email: string;
  firstName: string;
  lastName: string;
  isValid: boolean;
}

export interface AccountActivationPayload {
  token: string | null;
  password: string;
}

export interface PasswordResetPayload {
  email: string;
  token: string;
  newPassword: string;
}

@Injectable({
  providedIn: 'root'
})
export class OnboardingService {
  private apiUrl = `${environment.apiUrl}/auth/onboarding`;
  private http = inject(HttpClient);

  submitWaitlist(registration: WaitlistRegistration): Observable<string> {
    return this.http.post<string>(`${this.apiUrl}/waitlist`, registration);
  }

  getKurinNumberRange(): Observable<KurinNumberRange> {
    return this.http.get<KurinNumberRange>(`${this.apiUrl}/kurin-numbers`);
  }

  /** The default край first, the rest by name in Ukrainian order. */
  getKrais(): Observable<KraiOption[]> {
    return this.http.get<KraiOption[]>(`${this.apiUrl}/krais`).pipe(
      map(krais => [...krais].sort((a, b) =>
        Number(b.isDefault) - Number(a.isDefault) || kraiNameOrder.compare(a.name, b.name)))
    );
  }

  getWaitlistEntries(): Observable<WaitlistEntry[]> {
    return this.http.get<WaitlistEntry[]>(`${this.apiUrl}/waitlist`);
  }

  /** Without a край the kurin opens in the one the applicant picked, or in the default. */
  approveWaitlistEntry(key: string, kraiCode?: string | null): Observable<string> {
    return this.http.post<string>(`${this.apiUrl}/waitlist/${key}/approve`, kraiCode ? { kraiCode } : {});
  }

  rejectWaitlistEntry(key: string, note?: string): Observable<string> {
    return this.http.post<string>(`${this.apiUrl}/waitlist/${key}/reject`, JSON.stringify(note), {
      headers: { 'Content-Type': 'application/json' }
    });
  }

  resendInvitation(key: string): Observable<string> {
    return this.http.post<string>(`${this.apiUrl}/waitlist/${key}/resend-invitation`, {});
  }

  validateInvitation(token: string): Observable<InvitationValidationResponse> {
    return this.http.get<InvitationValidationResponse>(`${this.apiUrl}/invitation/${token}/validate`);
  }

  /** Answers with a signed-in session: the person lands inside the app, not on the sign-in form. */
  activateAccount(payload: AccountActivationPayload): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/activate`, payload, { withCredentials: true });
  }

  requestPasswordReset(email: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.apiUrl}/password-reset/request`, { email }, { context: requestFeedback('silent') });
  }

  resendInvitationByEmail(email: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.apiUrl}/invitation/resend`, { email }, { context: requestFeedback('silent') });
  }

  resetPassword(payload: PasswordResetPayload): Observable<boolean> {
    return this.http.post<boolean>(`${this.apiUrl}/password-reset/reset`, payload);
  }
}
