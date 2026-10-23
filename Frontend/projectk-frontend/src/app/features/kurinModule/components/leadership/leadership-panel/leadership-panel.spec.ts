import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { Table } from '@openng/optimus-ui/table';
import { PermissionService } from '../../../../authModule/services/permission-service/permission.service';
import { LeadershipService } from '../../../services/leadership-service/leadership.service';
import { LeadershipPanelComponent } from './leadership-panel';

describe('LeadershipPanelComponent', () => {
  let component: LeadershipPanelComponent;
  let fixture: ComponentFixture<LeadershipPanelComponent>;

  beforeEach(async () => {
    const leadershipService = {
      getLeadershipByTypeAndKey: vi.fn()
    };
    leadershipService.getLeadershipByTypeAndKey.mockReturnValue(of({
      leadershipKey: 'leadership-1',
      type: 'group',
      entityKey: 'group-1',
      startDate: '2026-01-01',
      endDate: null,
      leadershipHistories: []
    }));

    await TestBed.configureTestingModule({
      imports: [LeadershipPanelComponent],
      providers: [
        { provide: LeadershipService, useValue: leadershipService },
        {
          provide: PermissionService,
          useValue: { canSetupLeadership: () => false }
        },
        {
          provide: Router,
          useValue: {
            navigate: vi.fn()
        }
      }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LeadershipPanelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should keep the regular table unconstrained when archive is hidden', () => {
    const table = fixture.debugElement.query(By.directive(Table)).componentInstance as Table;

    expect(table.scrollable).toBe(false);
    expect(table.scrollHeight).toBeUndefined();
  });

  it('should constrain archive mode to a seven-row scroll viewport', () => {
    component.showArchived = true;
    fixture.detectChanges();

    const table = fixture.debugElement.query(By.directive(Table)).componentInstance as Table;
    expect(table.scrollable).toBe(true);
    expect(table.scrollHeight).toBe('27.5rem');
  });
});
