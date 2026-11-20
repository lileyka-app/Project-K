import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { MemberAwardsDialogComponent } from './member-awards-dialog';

describe('MemberAwardsDialogComponent', () => {
  let fixture: ComponentFixture<MemberAwardsDialogComponent>;
  let component: MemberAwardsDialogComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MemberAwardsDialogComponent],
      providers: [provideNoopAnimations()]
    });
    fixture = TestBed.createComponent(MemberAwardsDialogComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('canEdit', true);
    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();
  });

  // The card hands the awards in as a fresh array on every change detection. Resetting the form on
  // that wiped the date the moment it was picked in the calendar.
  it('keeps a picked date when the list of awards comes in again', () => {
    const picked = new Date(2026, 10, 10);
    component.form.get('dateAcquired')!.setValue(picked);

    fixture.componentRef.setInput('existingAwards', []);
    fixture.detectChanges();

    expect(component.form.get('dateAcquired')!.value).toEqual(picked);
  });

  it('starts empty again when it opens anew', () => {
    component.form.get('dateAcquired')!.setValue(new Date(2026, 10, 10));

    fixture.componentRef.setInput('visible', false);
    fixture.detectChanges();
    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();

    expect(component.form.get('dateAcquired')!.value).toBeNull();
  });
});
