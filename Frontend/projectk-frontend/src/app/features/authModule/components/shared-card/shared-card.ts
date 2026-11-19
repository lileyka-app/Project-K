import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { environment } from '../../../../../environments/environment';
import { kurinInText } from '../../../../shared/functions/kurin-label.function';
import { CardUlad, PublicCardDto } from '../../services/card-service/card.service';

type SharedAward = NonNullable<PublicCardDto['awards']>[number];
type SharedBadge = NonNullable<PublicCardDto['badges']>[number];

const AWARD_NAMES: Record<1 | 2 | 3 | 4, string> = {
  1: 'Перше відзначення',
  2: 'Друге відзначення',
  3: 'Третє відзначення',
  4: 'Четверте відзначення'
};

const ULAD_ORDER: (CardUlad | null)[] = ['UPS', 'USP', null, 'UPYu'];
const ULAD_NAMES: Record<CardUlad | 'senior', string> = { UPYu: 'УПЮ', USP: 'УСП', UPS: 'УПС', senior: 'УСП і УПС' };

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

  /**
   * The відзначення by the ulad they were earned in, the latest line first. One line needs no
   * heading; the whole way gets one per line.
   */
  readonly awardGroups = computed(() => {
    const awards = this.card().awards ?? [];
    const groups = ULAD_ORDER
      .map(ulad => ({ ulad, awards: awards.filter(a => (a.ulad ?? null) === ulad) }))
      .filter(group => group.awards.length > 0);
    return groups.map(group => ({
      label: groups.length > 1 ? ULAD_NAMES[group.ulad ?? 'senior'] : null,
      awards: group.awards
    }));
  });

  awardName(award: SharedAward): string {
    return award.level ? AWARD_NAMES[award.level] : award.title;
  }

  awardImage(award: SharedAward): string | null {
    return award.level ? `${environment.apiUrl}/awards/images/${award.level}?colored=true` : award.imageUrl;
  }

  /**
   * The catalogue's picture of the вмілість. Only a full address: the card is read by anyone, signed in
   * or not, and a path into the app's own store asks for a sign-in the reader does not have.
   */
  badgeImage(badge: SharedBadge): string | null {
    return badge.imagePath && /^https?:\/\//.test(badge.imagePath) ? badge.imagePath : null;
  }

  /** A picture that fails to load leaves the name alone rather than a broken image. */
  hideBroken(event: Event): void {
    (event.target as HTMLElement).hidden = true;
  }
}
