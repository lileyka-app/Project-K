import { Injectable, computed, inject, signal } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { OnboardingService, WaitlistEntry } from '../../../authModule/services/onboarding-service/onboarding.service';
import { KurinBranchRequestService } from '../../../kurinModule/services/kurin-branch-request-service/kurin-branch-request.service';
import { isWaitlistAwaitingDecision } from '../../functions/waitlist-status.function';

/**
 * How many applications and kurins' requests to change their гілки wait for an administrator's
 * decision. The sidebar and the admin panel show a dot from it; the waitlist page feeds it back
 * after every load, so a decision takes the dot away without another request.
 */
@Injectable({ providedIn: 'root' })
export class WaitlistAttentionService {
  private readonly onboarding = inject(OnboardingService);
  private readonly branchRequests = inject(KurinBranchRequestService);

  private readonly applications = signal(0);
  private readonly requests = signal(0);

  readonly pending = computed(() => this.applications() + this.requests());
  readonly pending$ = toObservable(this.pending);

  refresh(): void {
    this.onboarding.getWaitlistEntries().subscribe({
      next: entries => this.countFrom(entries),
      // A failed read is not worth a toast here; the page itself reports it when opened.
      error: () => undefined
    });
    this.branchRequests.getPending().subscribe({
      next: requests => this.countRequests(requests.length),
      error: () => undefined
    });
  }

  countFrom(entries: WaitlistEntry[]): void {
    this.applications.set(entries.filter(entry => isWaitlistAwaitingDecision(entry.verificationStatus)).length);
  }

  countRequests(count: number): void {
    this.requests.set(count);
  }
}
