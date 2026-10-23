import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MemberAwardsTileComponent } from './member-awards-tile';
import { BadgeImageBlobService } from '../../../../services/probes-and-badges/badge-image-blob.service';
import { MemberAwardDto } from '../../../../models/member-award.dto';
import { MemberAwardLevel } from '../../../../models/enums/member-award-level.enum';
import { BadgeProgressStatus } from '../../../../models/enums/badge-progress-status.enum';

describe('MemberAwardsTileComponent', () => {
  let fixture: ComponentFixture<MemberAwardsTileComponent>;
  let component: MemberAwardsTileComponent;

  function award(level: MemberAwardLevel, status: BadgeProgressStatus, submittedBy = 'someone'): MemberAwardDto {
    return {
      memberAwardKey: `a-${level}-${status}`,
      memberKey: 'm1',
      kurinKey: 'k1',
      level,
      dateAcquired: '2026-05-01',
      status,
      submittedByUserKey: submittedBy
    };
  }

  function create(inputs: Record<string, unknown>): void {
    fixture = TestBed.createComponent(MemberAwardsTileComponent);
    component = fixture.componentInstance;
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MemberAwardsTileComponent],
      providers: [{ provide: BadgeImageBlobService, useValue: { resolveBadgeImageForDisplay: (url: string | null) => url } }]
    }).compileComponents();
  });

  it('confirms only the levels the kurin gives the viewer', () => {
    create({ confirmLevels: [1, 2] });

    expect(component.canConfirm(award(MemberAwardLevel.Second, BadgeProgressStatus.Submitted))).toBe(true);
    expect(component.canConfirm(award(MemberAwardLevel.Third, BadgeProgressStatus.Submitted))).toBe(false);
    expect(component.canConfirm(award(MemberAwardLevel.First, BadgeProgressStatus.Confirmed))).toBe(false);
  });

  it('changes a confirmed award only with the right to remove one', () => {
    create({ canEdit: true });
    expect(component.canEditAward(award(MemberAwardLevel.First, BadgeProgressStatus.Confirmed))).toBe(false);
    expect(component.canEditAward(award(MemberAwardLevel.First, BadgeProgressStatus.Submitted))).toBe(true);

    create({ canEdit: true, canRemove: true });
    expect(component.canEditAward(award(MemberAwardLevel.First, BadgeProgressStatus.Confirmed))).toBe(true);
  });

  it('lets the submitter and the member withdraw what still waits, and nobody else without removal rights', () => {
    create({ viewerUserKey: 'ME' });
    expect(component.canDeleteAward(award(MemberAwardLevel.First, BadgeProgressStatus.Submitted, 'me'))).toBe(true);
    expect(component.canDeleteAward(award(MemberAwardLevel.First, BadgeProgressStatus.Submitted))).toBe(false);
    expect(component.canDeleteAward(award(MemberAwardLevel.First, BadgeProgressStatus.Confirmed, 'me'))).toBe(false);

    create({ ownCard: true });
    expect(component.canDeleteAward(award(MemberAwardLevel.First, BadgeProgressStatus.Submitted))).toBe(true);

    create({ canRemove: true });
    expect(component.canDeleteAward(award(MemberAwardLevel.First, BadgeProgressStatus.Confirmed))).toBe(true);
  });

  it('opens an award for whoever may confirm it, even without the right to change the card', () => {
    const waiting = award(MemberAwardLevel.First, BadgeProgressStatus.Submitted);
    create({ awards: [waiting], confirmLevels: [1] });

    component.openDialog(waiting);

    expect(component.dialogVisible).toBe(true);
  });
});
