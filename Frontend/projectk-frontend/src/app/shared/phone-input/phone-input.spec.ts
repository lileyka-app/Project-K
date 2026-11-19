import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { PhoneInputComponent } from './phone-input';

describe('PhoneInputComponent', () => {
  function create() {
    const fixture = TestBed.createComponent(PhoneInputComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('says a number is too long as soon as it is', () => {
    const fixture = create();
    fixture.componentInstance.setNational('050 123 45 67 89 01 23');
    fixture.detectChanges();
    expect(fixture.componentInstance.shownIssue()).toBe('long');
    expect(fixture.nativeElement.textContent).toContain('Задовгий номер.');
  });

  it('says a number is too short only once the box is left', () => {
    const fixture = create();
    fixture.componentInstance.setNational('12');
    expect(fixture.componentInstance.shownIssue()).toBeNull();
    fixture.componentInstance.touched();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Закороткий номер.');
  });

  it('takes a number of any country within 7 to 15 digits', () => {
    const fixture = create();
    fixture.componentInstance.setCountry('CZ');
    fixture.componentInstance.setNational('777 123 456 78');
    fixture.componentInstance.touched();
    expect(fixture.componentInstance.shownIssue()).toBeNull();
    expect(fixture.componentInstance.validate(new FormControl('+42077712345678'))).toBeNull();
    expect(fixture.componentInstance.validate(new FormControl('+4207771234567890'))).toEqual({ phone: 'long' });
  });
});
