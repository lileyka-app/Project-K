import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { environment } from '../../../../../environments/environment';
import { kurinInText } from '../../../../shared/functions/kurin-label.function';
import { PublicCardDto } from '../../services/card-service/card.service';

type SharedAward = NonNullable<PublicCardDto['awards']>[number];

const AWARD_NAMES: Record<1 | 2 | 3 | 4, string> = {
  1: 'Перше відзначення',
  2: 'Друге відзначення',
  3: 'Третє відзначення',
  4: 'Четверте відзначення'
};

/**
 * A shared member card as others see it: the name and only the parts the person chose to show. The
 * same picture on the public page, in the person's own preview, and after a scan.
 */
@Component({
  selector: 'app-shared-card',
  imports: [DatePipe],
  templateUrl: './shared-card.html',
  styleUrl: './shared-card.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SharedCardComponent {
  readonly card = input.required<PublicCardDto>();

  readonly initials = computed(() =>
    this.card().fullName.split(' ').filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase());

  readonly kurinLine = computed(() =>
    (this.card().kurins ?? [])
      .map(k => [kurinInText(k.branches, k.number), k.namedAfter ? `ім. ${k.namedAfter}` : null, k.groupName ? `гурток «${k.groupName}»` : null]
        .filter(Boolean).join(' · '))
      .join('; '));

  awardName(award: SharedAward): string {
    return award.level ? AWARD_NAMES[award.level] : award.title;
  }

  awardImage(award: SharedAward): string | null {
    return award.level ? `${environment.apiUrl}/awards/images/${award.level}?colored=true` : award.imageUrl;
  }
}
