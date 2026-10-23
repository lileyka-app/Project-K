import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { map, Observable, of } from 'rxjs';
import { SelectModule } from '@openng/optimus-ui/select';
import { SelectButtonModule } from '@openng/optimus-ui/selectbutton';
import { TableModule } from '@openng/optimus-ui/table';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { KurinAccessPolicyDto, SetKurinAccessPolicyRequest } from '../../models/kurin-access-policy.dto';
import {
  AUDIENCE_LABELS,
  CAPABILITY_GROUP_LABELS,
  CAPABILITY_LABELS,
  CapabilityGroup,
  CapabilityReach,
  KurinCapability,
  labelOf,
  PolicyAudience,
  REACH_LABELS
} from '../../models/access-policy-labels.model';

/** One cell as the page shows it: the value set here, its default and whether it may change. */
export interface AccessCellView {
  audience: PolicyAudience;
  audienceLabel: string;
  value: CapabilityReach;
  defaultValue: CapabilityReach;
  locked: boolean;
  changed: boolean;
  /** «За замовчуванням: …», for the mark on a changed cell. */
  defaultHint: string;
}

export interface AccessRowView {
  key: KurinCapability;
  label: string;
  /** The «Юнаки» cell is fixed at «—» on action rows; the phone leaves it out. */
  youthConfigurable: boolean;
  cells: AccessCellView[];
}

export interface AccessGroupView {
  group: CapabilityGroup;
  label: string;
  rows: AccessRowView[];
}

/** Where the matrix stops fitting and each section becomes a card (BRANDBOOK: tables turn on phones). */
const PHONE_QUERY = '(max-width: 767px)';

const REACHES: CapabilityReach[] = ['None', 'OwnGroup', 'Kurin'];

const cellKey = (capability: string, audience: string) => `${capability}/${audience}`;

/**
 * «Права й видимість» of the kurin: for each right, how far each audience reaches — nobody, their own
 * гурток, or the whole kurin. A matrix with a fixed first column on a computer, a card per section
 * with a switch per audience on a phone. Changes stay here until the settings page's one «Зберегти»;
 * «Як за замовчуванням» only resets what is on screen.
 */
@Component({
  selector: 'app-kurin-access-policy',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, SelectModule, SelectButtonModule, TableModule, ButtonModule, TooltipModule],
  templateUrl: './kurin-access-policy.html',
  styleUrl: './kurin-access-policy.css'
})
export class KurinAccessPolicyComponent implements OnInit {
  private readonly kurins = inject(KurinService);
  private readonly destroyRef = inject(DestroyRef);

  readonly kurinKey = input.required<string>();

  protected readonly reachOptions = REACHES.map(value => ({ value, label: REACH_LABELS[value] }));

  readonly loaded = signal(false);
  readonly failed = signal(false);
  /** The policy as the server last answered. */
  private readonly policy = signal<KurinAccessPolicyDto | null>(null);
  /** What is on screen, by `Capability/Audience`. */
  private readonly values = signal<Record<string, CapabilityReach>>({});
  readonly phone = signal(false);

  readonly audiences = computed(() => (this.policy()?.audiences ?? [])
    .map(audience => ({ audience, label: labelOf(AUDIENCE_LABELS, audience) })));

  readonly groups = computed<AccessGroupView[]>(() => {
    const policy = this.policy();
    if (!policy) {
      return [];
    }
    const values = this.values();
    const groups: AccessGroupView[] = [];
    for (const capability of policy.capabilities) {
      let group = groups.find(candidate => candidate.group === capability.group);
      if (!group) {
        group = { group: capability.group, label: labelOf(CAPABILITY_GROUP_LABELS, capability.group), rows: [] };
        groups.push(group);
      }
      group.rows.push({
        key: capability.key,
        label: labelOf(CAPABILITY_LABELS, capability.key),
        youthConfigurable: capability.youthConfigurable,
        cells: capability.cells.map(cell => {
          const value = values[cellKey(capability.key, cell.audience)] ?? cell.effective;
          return {
            audience: cell.audience,
            audienceLabel: labelOf(AUDIENCE_LABELS, cell.audience),
            value,
            defaultValue: cell.default,
            locked: cell.locked,
            changed: value !== cell.default,
            defaultHint: `За замовчуванням: ${labelOf(REACH_LABELS, cell.default)}`
          };
        })
      });
    }
    return groups;
  });

  /** Anything on screen that the server does not have yet. */
  readonly dirty = computed(() => {
    const values = this.values();
    return (this.policy()?.capabilities ?? []).some(capability => capability.cells
      .some(cell => (values[cellKey(capability.key, cell.audience)] ?? cell.effective) !== cell.effective));
  });

  /** Whether anything on screen differs from the defaults, i.e. whether «Як за замовчуванням» would do anything. */
  readonly customised = computed(() => this.groups()
    .some(group => group.rows.some(row => row.cells.some(cell => cell.changed))));

  ngOnInit(): void {
    this.watchWidth();
    this.kurins.getAccessPolicy(this.kurinKey()).subscribe({
      next: policy => {
        this.apply(policy);
        this.loaded.set(true);
      },
      error: () => {
        this.failed.set(true);
        this.loaded.set(true);
      }
    });
  }

  set(capability: KurinCapability, audience: PolicyAudience, reach: CapabilityReach | null): void {
    if (!reach || this.isLocked(capability, audience)) {
      return;
    }
    this.values.update(values => ({ ...values, [cellKey(capability, audience)]: reach }));
  }

  /** Puts every cell back to its default on screen; nothing is sent until «Зберегти». */
  resetToDefaults(): void {
    const values: Record<string, CapabilityReach> = {};
    for (const capability of this.policy()?.capabilities ?? []) {
      for (const cell of capability.cells) {
        values[cellKey(capability.key, cell.audience)] = cell.default;
      }
    }
    this.values.set(values);
  }

  /** Sends the whole matrix when something changed; nothing to send otherwise. */
  save(): Observable<void> {
    const policy = this.policy();
    if (!policy || !this.dirty()) {
      return of(undefined);
    }
    return this.kurins.setAccessPolicy(this.kurinKey(), this.request(policy)).pipe(map(saved => {
      this.apply(saved);
    }));
  }

  private request(policy: KurinAccessPolicyDto): SetKurinAccessPolicyRequest {
    const values = this.values();
    const grants: SetKurinAccessPolicyRequest['grants'] = {};
    for (const capability of policy.capabilities) {
      grants[capability.key] = Object.fromEntries(capability.cells.map(cell =>
        [cell.audience, values[cellKey(capability.key, cell.audience)] ?? cell.effective]));
    }
    return { grants };
  }

  private apply(policy: KurinAccessPolicyDto): void {
    this.policy.set(policy);
    const values: Record<string, CapabilityReach> = {};
    for (const capability of policy.capabilities) {
      for (const cell of capability.cells) {
        values[cellKey(capability.key, cell.audience)] = cell.effective;
      }
    }
    this.values.set(values);
  }

  private isLocked(capability: KurinCapability, audience: PolicyAudience): boolean {
    return this.policy()?.capabilities
      .find(candidate => candidate.key === capability)?.cells
      .find(cell => cell.audience === audience)?.locked ?? true;
  }

  private watchWidth(): void {
    const query = globalThis.matchMedia?.(PHONE_QUERY);
    if (!query) {
      return;
    }
    this.phone.set(query.matches);
    const onChange = (event: MediaQueryListEvent) => this.phone.set(event.matches);
    query.addEventListener?.('change', onChange);
    this.destroyRef.onDestroy(() => query.removeEventListener?.('change', onChange));
  }
}
