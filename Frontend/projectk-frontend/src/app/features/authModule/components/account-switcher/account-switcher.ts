import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TagModule } from '@openng/optimus-ui/tag';
import { ProgressSpinnerModule } from '@openng/optimus-ui/progressspinner';
import { TransferCodeDialogComponent } from '../transfer-code-dialog/transfer-code-dialog';
import { CardCheckDialogComponent } from '../card-check-dialog/card-check-dialog';
import { AuthService } from '../../services/auth-service/auth.service';
import { SavedAccount } from '../../models/saved-account.model';
import { authenticatedHomeRoute } from '../../functions/authenticated-home-route';
import { accountRoleTag, AccountRoleTag } from '../../../kurinModule/functions/account-role-tag.function';

interface SwitcherRow {
  account: SavedAccount;
  title: string;
  /** The email under the name; null when the name is the email. */
  subtitle: string | null;
  tag: AccountRoleTag;
}

/**
 * The signed-in account at the foot of the sidebar, and above it, once opened, the other accounts
 * this browser is signed in to and a way to add one. Switching reloads the page: every screen and
 * cache holds the previous account's data, and a fresh start is the one way to drop all of it.
 */
@Component({
  selector: 'app-account-switcher',
  imports: [TagModule, ProgressSpinnerModule, TransferCodeDialogComponent, CardCheckDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './account-switcher.html',
  styleUrl: './account-switcher.css'
})
export class AccountSwitcherComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly email = input<string | null>(null);
  readonly roleTag = input<AccountRoleTag | null>(null);
  /** The switcher led somewhere else, so whatever holds it (the drawer) can close. */
  readonly navigated = output<void>();

  readonly open = signal(false);
  readonly loading = signal(false);
  readonly rows = signal<SwitcherRow[]>([]);
  /** The account being switched to, while the server answers. */
  readonly switching = signal<string | null>(null);
  readonly codeVisible = signal(false);
  readonly cardVisible = signal(false);

  /** «Мій код для переходу» is a person's; an account with no member behind it (an admin) has none. */
  get hasOwnCode(): boolean {
    return !!this.authService.getAuthStateValue()?.memberKey;
  }

  showCode(): void {
    this.open.set(false);
    this.codeVisible.set(true);
  }

  /** «Показати картку»: the three-minute QR anyone may scan to check the person. */
  showCard(): void {
    this.open.set(false);
    this.cardVisible.set(true);
  }

  toggle(): void {
    const opening = !this.open();
    this.open.set(opening);
    if (opening) {
      this.load();
    }
  }

  switchTo(row: SwitcherRow): void {
    if (this.switching()) {
      return;
    }

    this.switching.set(row.account.userKey);
    this.authService.switchAccount(row.account.userKey).subscribe({
      next: state => {
        const home = this.router.serializeUrl(this.router.createUrlTree(authenticatedHomeRoute(state)));
        window.location.assign(home);
      },
      error: () => {
        // The kept session may have ended meanwhile (a password change ends them all); show what is left.
        this.switching.set(null);
        this.load();
      }
    });
  }

  addAccount(): void {
    this.open.set(false);
    this.navigated.emit();
    void this.router.navigate(['/login'], { queryParams: { add: 1 } });
  }

  private load(): void {
    this.loading.set(true);
    this.authService.getSavedAccounts().subscribe({
      next: accounts => {
        this.rows.set(accounts.map(toRow));
        this.loading.set(false);
      },
      error: () => {
        this.rows.set([]);
        this.loading.set(false);
      }
    });
  }
}

function toRow(account: SavedAccount): SwitcherRow {
  const name = account.fullName.trim();
  return {
    account,
    title: name || account.email,
    subtitle: name ? account.email : null,
    tag: accountRoleTag(account.isAdmin, account.roles)
  };
}
