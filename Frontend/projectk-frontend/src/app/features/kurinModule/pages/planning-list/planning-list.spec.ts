import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { Confirmation, ConfirmationService } from '@openng/optimus-ui/api';
import { PlanningListComponent } from './planning-list';
import { PlanningService } from '../../services/planning-service/planning.service';
import { MemberService } from '../../services/member-service/member.service';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { PlanningSessionDto } from '../../models/planning-session.dto';

describe('PlanningListComponent', () => {
  let fixture: ComponentFixture<PlanningListComponent>;
  let component: PlanningListComponent;
  let planning: MockedObject<PlanningService>;

  const session = { planningSessionKey: 'session-1', name: 'Зимовий табір', canDelete: true } as PlanningSessionDto;

  beforeEach(async () => {
    planning = {
      getSessions: vi.fn(),
      deleteSession: vi.fn()
    } as MockedObject<PlanningService>;
    planning.getSessions.mockReturnValue(of([session]));
    planning.deleteSession.mockReturnValue(of(''));

    await TestBed.configureTestingModule({
      imports: [PlanningListComponent],
      providers: [
        provideRouter([]),
        { provide: PlanningService, useValue: planning },
        { provide: MemberService, useValue: {} },
        { provide: PermissionService, useValue: { canCreatePlanning: () => true } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ kurinKey: 'kurin-1' })) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PlanningListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('deletes a session only once the confirmation is accepted', () => {
    const confirmations = fixture.debugElement.injector.get(ConfirmationService);
    let asked: Confirmation | undefined;
    vi.spyOn(confirmations, 'confirm').mockImplementation((confirmation: Confirmation) => {
      asked = confirmation;
      return confirmations;
    });

    component.delete(session);

    expect(asked?.message).toContain('Зимовий табір');
    expect(planning.deleteSession).not.toHaveBeenCalled();

    asked?.accept?.();

    expect(planning.deleteSession).toHaveBeenCalledWith('session-1');
    expect(planning.getSessions).toHaveBeenCalledTimes(2);
  });
});
