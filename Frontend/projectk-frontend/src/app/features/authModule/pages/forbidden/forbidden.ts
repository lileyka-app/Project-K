import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';

/**
 * Відмова. Причину бере з `data.reason` маршруту: «немає прав» і «у цьому курені такого немає» —
 * різні речі, і другу людина не виправить ні входом, ні роллю.
 */
@Component({
  selector: 'app-forbidden-component',
  imports: [RouterLink, ButtonModule],
  templateUrl: './forbidden.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './forbidden.css'
})
export class ForbiddenComponent {
  private readonly route = inject(ActivatedRoute).snapshot;
  readonly isNoYouthProgram = this.route.data['reason'] === 'youthProgram';
  /** Можливість, яку курінь вимкнув у налаштуваннях: «score». */
  readonly featureOff = this.route.data['reason'] === 'featureOff' ? this.route.paramMap.get('feature') : null;
}
