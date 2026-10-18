import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CheckboxModule } from '@openng/optimus-ui/checkbox';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { SelectModule } from '@openng/optimus-ui/select';
import {
  CONCESSION_KINDS,
  CONCESSION_KIND_LABELS,
  ConcessionKind,
  DUES_PARTS,
  DUES_PART_LABELS,
  DuesPart,
  KraiConcessionDocument
} from '../../../../duesModule/models/concession.dto';

/**
 * Пільги краю: назва, як вона знижує вкладку і які частини. Ключ створюється сам і не міняється —
 * на нього посилаються вже надані пільги. Зміна правила діє на пільги, надані після збереження.
 */
@Component({
  selector: 'app-krai-concessions',
  imports: [FormsModule, ButtonModule, CheckboxModule, InputTextModule, SelectModule],
  templateUrl: './krai-concessions.html',
  styleUrl: './krai-concessions.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KraiConcessionsComponent {
  readonly value = model<KraiConcessionDocument[] | undefined>(undefined);
  readonly disabled = input(false);
  /** «Kč», «zł»: how the край writes its currency, for a sum off. */
  readonly currencySymbol = input('₴');

  readonly kindOptions = CONCESSION_KINDS.map(kind => ({ kind, label: CONCESSION_KIND_LABELS[kind] }));
  readonly parts = DUES_PARTS.map(part => ({ part, label: DUES_PART_LABELS[part] }));

  readonly items = computed(() => this.value() ?? []);

  add(): void {
    this.set([...this.items(), {
      key: newKey(this.items()),
      name: '',
      kind: 'percent',
      value: 10,
      appliesTo: ['stanytsia']
    }]);
  }

  remove(index: number): void {
    this.set(this.items().filter((_, i) => i !== index));
  }

  update(index: number, patch: Partial<KraiConcessionDocument>): void {
    this.set(this.items().map((item, i) => i === index ? tidy({ ...item, ...patch }) : item));
  }

  setKind(index: number, kind: ConcessionKind): void {
    if (kind === 'exempt') {
      this.update(index, { kind, value: undefined });
      return;
    }
    // A percent and a sum are different units: «20» kept across the switch would mean something else.
    const item = this.items()[index];
    const startingValue = kind === 'percent' ? 10 : 100;
    this.update(index, { kind, value: item.kind === kind ? item.value ?? startingValue : startingValue });
  }

  setValue(index: number, value: string | number | null): void {
    const number = typeof value === 'number' ? value : Number(value);
    this.update(index, { value: Number.isFinite(number) ? number : undefined });
  }

  covers(item: KraiConcessionDocument, part: DuesPart): boolean {
    return item.appliesTo.includes(part);
  }

  setPart(index: number, part: DuesPart, on: boolean): void {
    const item = this.items()[index];
    const appliesTo = DUES_PARTS.filter(p => p === part ? on : item.appliesTo.includes(p));
    this.update(index, { appliesTo });
  }

  unit(item: KraiConcessionDocument): string {
    return item.kind === 'percent' ? '%' : this.currencySymbol();
  }

  private set(items: KraiConcessionDocument[]): void {
    this.value.set(items.length ? items : undefined);
  }
}

function tidy(item: KraiConcessionDocument): KraiConcessionDocument {
  return item.kind === 'exempt' ? { key: item.key, name: item.name, kind: item.kind, appliesTo: item.appliesTo } : item;
}

/** A key the grants can point at: short, lowercase, and not taken in this край. */
function newKey(items: KraiConcessionDocument[]): string {
  const taken = new Set(items.map(item => item.key));
  let key: string;
  do {
    key = `p-${crypto.randomUUID().slice(0, 8)}`;
  } while (taken.has(key));
  return key;
}
