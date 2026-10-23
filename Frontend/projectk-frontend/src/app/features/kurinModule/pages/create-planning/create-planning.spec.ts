import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { MemberService } from '../../services/member-service/member.service';
import { PlanningService } from '../../services/planning-service/planning.service';

import { CreatePlanningComponent } from './create-planning';

describe('CreatePlanning', () => {
  let component: CreatePlanningComponent;
  let fixture: ComponentFixture<CreatePlanningComponent>;

  beforeEach(async () => {
    const planningServiceSpy = {
      createSession: vi.fn()
    };
    planningServiceSpy.createSession.mockReturnValue(of({}));

    const memberServiceSpy = {
      getKVMembers: vi.fn()
    };
    memberServiceSpy.getKVMembers.mockReturnValue(of([]));

    const routerSpy = {
      navigate: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [CreatePlanningComponent],
      providers: [
        { provide: PlanningService, useValue: planningServiceSpy },
        { provide: MemberService, useValue: memberServiceSpy },
        { provide: Router, useValue: routerSpy },
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({}))
          }
        }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CreatePlanningComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
