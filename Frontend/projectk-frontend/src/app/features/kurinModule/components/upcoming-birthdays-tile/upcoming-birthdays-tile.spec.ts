import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UpcomingBirthdaysTileComponent } from './upcoming-birthdays-tile';
import { MemberLookupDto } from '../../models/requests/member/member-lookup.dto';

describe('UpcomingBirthdaysTileComponent', () => {
  let component: UpcomingBirthdaysTileComponent;
  let fixture: ComponentFixture<UpcomingBirthdaysTileComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UpcomingBirthdaysTileComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(UpcomingBirthdaysTileComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should include only birthdays in next 30 days and sort by nearest date', () => {
    const referenceDate = new Date('2026-04-06T00:00:00');
    fixture.componentRef.setInput('daysAhead', 30);
    fixture.componentRef.setInput('members', [
      { memberKey: 'a', firstName: 'Today', lastName: 'Person', middleName: '', dateOfBirth: '2000-04-06' } as MemberLookupDto,
      { memberKey: 'b', firstName: 'Future', lastName: 'Person', middleName: '', dateOfBirth: '2000-04-20' } as MemberLookupDto,
      { memberKey: 'c', firstName: 'Border', lastName: 'Person', middleName: '', dateOfBirth: '2000-05-06' } as MemberLookupDto,
      { memberKey: 'd', firstName: 'Outside', lastName: 'Person', middleName: '', dateOfBirth: '2000-05-07' } as MemberLookupDto
    ]);

    const result = component.buildUpcomingBirthdays(referenceDate);

    expect(result).toHaveLength(3);
    expect(result.map(item => item.member.memberKey)).toEqual(['a', 'b', 'c']);
    expect(result.map(item => item.daysUntilBirthday)).toEqual([0, 14, 30]);
  });

  it('should ignore invalid or empty dateOfBirth values', () => {
    const referenceDate = new Date('2026-04-06T00:00:00');
    fixture.componentRef.setInput('members', [
      { memberKey: 'a', firstName: 'NoDate', lastName: 'One', middleName: '', dateOfBirth: null } as MemberLookupDto,
      { memberKey: 'b', firstName: 'Invalid', lastName: 'Two', middleName: '', dateOfBirth: 'not-a-date' } as MemberLookupDto
    ]);

    const result = component.buildUpcomingBirthdays(referenceDate);

    expect(result).toHaveLength(0);
  });

  it('should read the day and month from birthday when the year is hidden', () => {
    const referenceDate = new Date('2026-04-06T00:00:00');
    fixture.componentRef.setInput('members', [
      { memberKey: 'a', firstName: 'Hidden', lastName: 'Year', middleName: '', dateOfBirth: null, birthday: '04-10' } as MemberLookupDto,
      { memberKey: 'b', firstName: 'Full', lastName: 'Date', middleName: '', dateOfBirth: '2000-04-08', birthday: '04-08' } as MemberLookupDto
    ]);

    const result = component.buildUpcomingBirthdays(referenceDate);

    expect(result.map(item => item.member.memberKey)).toEqual(['b', 'a']);
    expect(result.map(item => item.daysUntilBirthday)).toEqual([2, 4]);
  });
});