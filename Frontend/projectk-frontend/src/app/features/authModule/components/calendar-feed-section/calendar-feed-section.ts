import { ChangeDetectionStrategy, Component, computed, ElementRef, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { ButtonModule } from '@openng/optimus-ui/button';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { TagModule } from '@openng/optimus-ui/tag';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { LocalUtcDatePipe } from '../../../../shared/pipes/local-utc-date.pipe';
import {
  CalendarFeedOptions, CalendarFeedService, CalendarFeedStatus, googleCalendarUrlOf, revokeReasonText, webcalUrlOf
} from '../../services/calendar-feed-service/calendar-feed.service';

/**
 * «Календар у Google, Apple, Outlook» in the account settings: issue the secret link, choose what it
 * carries, replace or switch it off. The link is shown only right after it is issued — the server
 * keeps its hash alone — so a lost link is reissued, never read back.
 */
@Component({
  selector: 'app-calendar-feed-section',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonModule, ConfirmDialogModule, InputTextModule, MessageModule, TagModule, ToggleSwitchModule, LocalUtcDatePipe],
  providers: [ConfirmationService],
  templateUrl: './calendar-feed-section.html',
  styleUrl: './calendar-feed-section.css'
})
export class CalendarFeedSectionComponent implements OnInit {
  private readonly feeds = inject(CalendarFeedService);
  private readonly messages = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly route = inject(ActivatedRoute);
  private readonly host = inject(ElementRef<HTMLElement>);

  protected readonly status = signal<CalendarFeedStatus | null>(null);
  /** Set only by an issue in this visit; gone on reload, as the server cannot give it back. */
  protected readonly feedUrl = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected options: CalendarFeedOptions = { includeSchedules: false, includeTasks: false };

  protected readonly isActive = computed(() => this.status()?.state === 'Active');
  protected readonly revokedBySecurity = computed(() => {
    const status = this.status();
    return status?.state === 'Revoked' && (status.revokedReason === 'SecurityChange' || status.revokedReason === 'Suspended');
  });
  protected readonly revokedText = computed(() => revokeReasonText(this.status()?.revokedReason ?? null));
  protected readonly googleUrl = computed(() => (this.feedUrl() ? googleCalendarUrlOf(this.feedUrl()!) : null));
  protected readonly webcalUrl = computed(() => (this.feedUrl() ? webcalUrlOf(this.feedUrl()!) : null));

  ngOnInit(): void {
    this.feeds.getStatus().subscribe(status => {
      this.show(status);
      this.scrollIntoViewIfAsked();
    });
  }

  protected issue(): void {
    if (!this.isActive()) {
      this.doIssue();
      return;
    }
    this.confirmation.confirm({
      header: 'Створити нове посилання?',
      message: 'Старе посилання перестане оновлюватися. Його доведеться видалити з календаря й додати нове.',
      icon: 'pi pi-refresh',
      acceptLabel: 'Створити нове',
      rejectLabel: 'Скасувати',
      accept: () => this.doIssue()
    });
  }

  protected saveOptions(): void {
    if (!this.isActive()) {
      return;
    }
    this.feeds.update(this.options).subscribe(status => {
      this.show(status);
      this.messages.add({ severity: 'success', summary: 'Збережено', detail: 'Календар підхопить зміни під час наступного оновлення.' });
    });
  }

  protected disable(): void {
    this.confirmation.confirm({
      header: 'Вимкнути посилання?',
      message: 'Календар, підписаний на нього, перестане отримувати події з Лілейки.',
      icon: 'pi pi-ban',
      acceptLabel: 'Вимкнути',
      rejectLabel: 'Скасувати',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.busy.set(true);
        this.feeds.disable().subscribe({
          next: status => {
            this.feedUrl.set(null);
            this.show(status);
          },
          complete: () => this.busy.set(false),
          error: () => this.busy.set(false)
        });
      }
    });
  }

  protected async copy(): Promise<void> {
    const url = this.feedUrl();
    if (!url) {
      return;
    }
    try {
      await navigator.clipboard.writeText(url);
      this.messages.add({ severity: 'success', summary: 'Посилання скопійовано', detail: 'Встав його в календар як підписку за URL.' });
    } catch {
      this.messages.add({ severity: 'warn', summary: 'Не вдалося скопіювати', detail: 'Виділи посилання в полі й скопіюй вручну.' });
    }
  }

  protected openGoogle(): void {
    const url = this.googleUrl();
    if (url) {
      window.open(url, '_blank', 'noopener');
    }
  }

  /** The system hands `webcal://` to the calendar app, which asks to subscribe. */
  protected openWebcal(): void {
    const url = this.webcalUrl();
    if (url) {
      window.location.href = url;
    }
  }

  private doIssue(): void {
    this.busy.set(true);
    this.feeds.issue(this.options).subscribe({
      next: issued => {
        this.feedUrl.set(issued.feedUrl);
        this.show(issued.status);
      },
      complete: () => this.busy.set(false),
      error: () => this.busy.set(false)
    });
  }

  /**
   * `#calendar` (the «Підписатися» button, the notice that a link went off) names this section. The
   * router's anchor scrolling runs before the page has loaded, and this section is the last part of it
   * to fill in, so it scrolls itself once it has.
   */
  private scrollIntoViewIfAsked(): void {
    if (this.route.snapshot.fragment === 'calendar') {
      setTimeout(() => (this.host.nativeElement as HTMLElement).closest('#calendar')?.scrollIntoView({ block: 'start' }));
    }
  }

  private show(status: CalendarFeedStatus): void {
    this.status.set(status);
    if (status.state === 'Active') {
      this.options = { includeSchedules: status.includeSchedules, includeTasks: status.includeTasks };
    }
  }
}
