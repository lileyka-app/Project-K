import { inject, Injectable } from '@angular/core';
import { AuthService } from '../auth-service/auth.service';

/** The three провід bodies a leadership record belongs to. */
export type LeadershipScope = 'kv' | 'kurin' | 'group';

/**
 * Central UI gate. Every predicate is derived from the current user's backend permission strings
 * (format `Resource:Action:Scope`, e.g. `Group:Manage:KurinWide`), so nothing here inspects role
 * names. The backend enforces the same permissions with scope on every request.
 */
@Injectable({
  providedIn: 'root'
})
export class PermissionService {
  private readonly authService = inject(AuthService);

  private permissions(): string[] {
    return this.authService.getAuthStateValue?.()?.permissions ?? [];
  }

  private has(prefix: string): boolean {
    return this.permissions().some(permission => permission.startsWith(prefix));
  }

  isAdmin(): boolean {
    return this.authService.getAuthStateValue?.()?.isAdmin ?? false;
  }

  /** Whole-kurin managers: Зв'язковий and admin. */
  canManageWholeKurin(): boolean {
    return this.isAdmin() || this.has('Group:Manage:KurinWide');
  }

  /** Runs a гурток: Виховник and above. Провід offices are not included — they lead, they do not moderate. */
  canLeadGroups(): boolean {
    return this.has('Group:Update');
  }

  isReviewer(): boolean {
    return this.canLeadGroups() || this.canManageWholeKurin();
  }

  canManageGroups(): boolean {
    return this.canManageWholeKurin();
  }

  canManageMembers(): boolean {
    return this.isReviewer();
  }

  canManageMentors(): boolean {
    return this.canManageWholeKurin();
  }

  /**
   * Who seats a провід, per body, the way the backend's AssignableOffices has it: Звʼязковий and
   * admin every one; Курінний only the kurin провід; Виховник and Гуртковий only a гурток's. Checking
   * `Leadership:Update` alone would let a Гуртковий open the kurin провід form and meet a 403.
   */
  canSetupLeadership(type: LeadershipScope): boolean {
    if (this.isAdmin() || this.has('Leadership:Manage:KurinWide')) {
      return true;
    }
    switch (type) {
      case 'kv':
        return false;
      case 'kurin':
        return this.has('Leadership:Update:KurinWide');
      case 'group':
        return this.has('Leadership:Update:OwnGroups');
    }
  }

  /**
   * Confirms вмілості somewhere: whoever the kurin's policy gives `BadgeProgress:Update`, at any
   * scope. By default that is the Звʼязковий and the впорядники, but a kurin may give it to its
   * провід or take it from someone who leads a гурток, so leading one is no answer here.
   */
  canReviewSkills(): boolean {
    return this.isAdmin() || this.has('BadgeProgress:Update');
  }

  /** Reads the kurin's «Журнал дій»: the КВ by default, whoever else the kurin's policy names. */
  canReadJournal(): boolean {
    return this.isAdmin() || this.has('KurinJournal:Read');
  }

  /** Opening a planning session: the whole провід, each within their own scope. */
  canCreatePlanning(): boolean {
    return this.isAdmin() || this.has('PlanningSession:Create');
  }

  // The whole провід raises agenda items; the backend decides who may edit one afterwards
  // (its author, or the Виховник of a гурток it targets).
  canManageAgenda(): boolean {
    return this.isAdmin() || this.has('AgendaItem:Create');
  }

  canManageKurinSettings(): boolean {
    return this.isAdmin() || this.has('Kurin:Update:KurinWide');
  }

  /**
   * Keeps or oversees the kurin's box: the курінний скарбник and the Звʼязковий. Youth get no
   * `KurinDues` grant at all — their `GroupDues:Read:Own` is about their own balance only.
   */
  canSeeKurinDues(): boolean {
    return this.isAdmin() || this.has('KurinDues:Read');
  }

  /**
   * Keeps some гурток's box — Виховник, гуртковий, скарбник гуртка — or reads them all. A youth's
   * `GroupDues:Read:Own` is about their own balance, not a box, so it does not count.
   */
  canSeeGroupDues(): boolean {
    return this.isAdmin() || this.has('GroupDues:Read:OwnGroups') || this.has('GroupDues:Read:KurinWide');
  }

  /**
   * Scores somewhere — a суддя, гуртковий, курінний, Виховник or the Звʼязковий. A youth's
   * `GroupScore:Read:Own` is about their own points, so it does not count.
   */
  canScore(): boolean {
    return this.isAdmin() || this.has('GroupScore:Create');
  }

  /** Opens the КВ's private book: the Звʼязковий and the впорядники, nobody else at any scope. */
  canSeePrivateScore(): boolean {
    return this.isAdmin() || this.has('KurinScorePrivate:Read');
  }

  /** Sets how the kurin scores: the суддя куреня and the Звʼязковий. */
  canManageScore(): boolean {
    return this.isAdmin() || this.has('KurinScore:Manage');
  }

  getRoleSeverity(): string {
    if (this.isAdmin()) {
      return 'danger';
    }
    if (this.canManageWholeKurin()) {
      return 'warn';
    }
    if (this.canLeadGroups()) {
      return 'success';
    }
    return 'info';
  }
}
