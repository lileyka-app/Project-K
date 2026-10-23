import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MiniMemberCardComponent } from './mini-member-card';
import { MemberLookupDto } from '../../models/requests/member/member-lookup.dto';

describe('MiniMemberCardComponent', () => {
  let component: MiniMemberCardComponent;
  let fixture: ComponentFixture<MiniMemberCardComponent>;

  const member: MemberLookupDto = {
    memberKey: 'm1',
    firstName: 'John',
    middleName: 'M',
    lastName: 'Doe',
    latestPlastLevelDisplay: 'пл. уч.',
    phoneNumber: '+380000000',
    dateOfBirth: '2000-01-01'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MiniMemberCardComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(MiniMemberCardComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('member', member);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should emit navigate event', () => {
    vi.spyOn(component.navigate, 'emit').mockReturnValue(undefined);
    component.onNavigate();
    expect(component.navigate.emit).toHaveBeenCalledWith(member);
  });
});