import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { MemberWarningsDialogComponent, WARNING_STEPS } from './member-warnings-dialog';
import { MemberWarningService } from '../../../../services/member-warning-service/member-warning.service';
import { MemberWarningLevel } from '../../../../models/enums/member-warning-level.enum';
import { MemberWarningDto } from '../../../../models/member-warning.dto';

describe('MemberWarningsDialogComponent', () => {
  let fixture: ComponentFixture<MemberWarningsDialogComponent>;
  let component: MemberWarningsDialogComponent;
  let warningServiceSpy: MockedObject<MemberWarningService>;
  let confirmation: ConfirmationService;

  const inAMonth = new Date(Date.now() + 30 * 86400000).toISOString();
  const lastYear = new Date(Date.now() - 365 * 86400000).toISOString();

  function warning(level: MemberWarningLevel, issuedBy = 'me', expiresAtUtc = inAMonth): MemberWarningDto {
    return { memberWarningKey: `w-${level}`, memberKey: 'm1', level, issuedAtUtc: lastYear, expiresAtUtc, issuedByUserKey: issuedBy };
  }

  beforeEach(async () => {
    warningServiceSpy = {
      assignWarning: vi.fn(),
      cancelWarning: vi.fn()
    } as MockedObject<MemberWarningService>;
    warningServiceSpy.assignWarning.mockReturnValue(of(warning(MemberWarningLevel.Level1)));
    warningServiceSpy.cancelWarning.mockReturnValue(of(warning(MemberWarningLevel.Level1)));

    await TestBed.configureTestingModule({
      imports: [MemberWarningsDialogComponent],
      providers: [
        ConfirmationService,
        MessageService,
        { provide: MemberWarningService, useValue: warningServiceSpy }
      ]
    }).compileComponents();

    confirmation = TestBed.inject(ConfirmationService);
    vi.spyOn(confirmation, 'confirm').mockImplementation(options => {
      options.accept?.();
      return confirmation;
    });
  });

  function create(warnings: MemberWarningDto[]) {
    fixture = TestBed.createComponent(MemberWarningsDialogComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('memberKey', 'm1');
    fixture.componentRef.setInput('warnings', warnings);
    fixture.componentRef.setInput('currentUserKey', 'ME');
    fixture.detectChanges();
  }

  it('opens only the first when none stands', () => {
    create([]);
    expect(WARNING_STEPS.map(step => component.canGive(step.level))).toEqual([true, false, false]);
  });

  it('opens the next while the one before stands, and not after it ran out', () => {
    create([warning(MemberWarningLevel.Level1)]);
    expect(component.canGive(MemberWarningLevel.Level2)).toBe(true);

    create([warning(MemberWarningLevel.Level1, 'me', lastYear)]);
    expect(component.canGive(MemberWarningLevel.Level1)).toBe(true);
    expect(component.canGive(MemberWarningLevel.Level2)).toBe(false);
  });

  it('lets only whoever gave one cancel it', () => {
    create([warning(MemberWarningLevel.Level1), warning(MemberWarningLevel.Level2, 'someone-else')]);
    expect(component.canCancel(MemberWarningLevel.Level1)).toBe(true);
    expect(component.canCancel(MemberWarningLevel.Level2)).toBe(false);
  });

  it('gives one after asking and tells the card', () => {
    create([]);
    const changed = vi.spyOn(component.changed, 'emit').mockReturnValue(undefined);

    component.give(WARNING_STEPS[0]);

    expect(confirmation.confirm).toHaveBeenCalled();
    expect(warningServiceSpy.assignWarning).toHaveBeenCalledWith('m1', { level: MemberWarningLevel.Level1 });
    expect(changed).toHaveBeenCalled();
  });

  it('cancels one after asking', () => {
    create([warning(MemberWarningLevel.Level1)]);

    component.cancel(WARNING_STEPS[0]);

    expect(warningServiceSpy.cancelWarning).toHaveBeenCalledWith('m1', 'w-Level1');
  });
});
