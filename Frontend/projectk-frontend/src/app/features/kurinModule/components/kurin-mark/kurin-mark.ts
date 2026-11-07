import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { KurinNumberComponent } from '../kurin-number/kurin-number';
import { KurinBranch, isSeniorBranch } from '../../models/enums/kurin-branch.enum';

/**
 * Знак куреня там, де його показують квадратом: червона табличка з числом для УПЮ — це юнацька
 * відзнака, тож старший курінь показує натомість свою емблему як є, без кола й обведення. Курінь без
 * емблеми має спокійну заглушку того самого розміру, а не дірку. Розмір — `--kurin-mark-size`.
 */
@Component({
  selector: 'app-kurin-mark',
  imports: [KurinNumberComponent],
  templateUrl: './kurin-mark.html',
  styleUrl: './kurin-mark.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KurinMarkComponent {
  readonly number = input<number | null>(null);
  readonly branch = input<KurinBranch | null | undefined>(null);
  readonly emblemUrl = input<string | null | undefined>(null);
  /** Назва для читача екрана, коли знак — емблема. */
  readonly name = input<string | null | undefined>(null);

  protected readonly isSenior = computed(() => isSeniorBranch(this.branch()));
}
