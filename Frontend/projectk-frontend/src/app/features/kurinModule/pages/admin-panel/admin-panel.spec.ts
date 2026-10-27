import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminPanelComponent } from './admin-panel';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinDto } from '../../models/kurin.dto';
import { AuthState } from '../../../authModule/models/auth-state.model';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { MessageService } from '@openng/optimus-ui/api';
import { HttpErrorResponse } from '@angular/common/http';

describe('AdminPanelComponent', () => {
  let component: AdminPanelComponent;
  let fixture: ComponentFixture<AdminPanelComponent>;
  let kurinService: MockedObject<KurinService>;
  let authService: MockedObject<AuthService>;

  const mockKurins: KurinDto[] = [
    { kurinKey: '1', number: 101 },
    { kurinKey: '2', number: 102 }
  ];

  const scopedState: AuthState = {
    userKey: 'user-1',
    memberKey: null,
    email: 'admin@projectk.com',
    isAdmin: true, permissions: [], roles: ['Admin'],
    kurinKey: '1',
    accessToken: 'scoped-token'
  };

  beforeEach(async () => {
    const kurinServiceSpy = {
      getKurins: vi.fn(),
      createKurin: vi.fn(),
      inviteKurinFounder: vi.fn(),
      updateKurin: vi.fn(),
      deleteKurin: vi.fn(),
      getKrais: vi.fn(),
      setKurinKrai: vi.fn()
    };
    const authServiceSpy = {
      setKurinKey: vi.fn(),
      setKurinScope: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [AdminPanelComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: KurinService, useValue: kurinServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
        MessageService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminPanelComponent);
    component = fixture.componentInstance;
    kurinService = TestBed.inject(KurinService) as MockedObject<KurinService>;
    authService = TestBed.inject(AuthService) as MockedObject<AuthService>;

    kurinService.getKurins.mockReturnValue(of(mockKurins));
    kurinService.getKrais.mockReturnValue(of([
      { code: 'cz', name: 'Чехія', kurinCount: 1 },
      { code: 'ua', name: 'Україна', kurinCount: 2 }
    ]));
  });

  describe('краї', () => {
    it('groups the kurins with Україна first and by number inside a край', () => {
      kurinService.getKurins.mockReturnValue(of([
        { kurinKey: 'c', number: 3, kraiCode: 'cz' },
        { kurinKey: 'b', number: 12, kraiCode: 'ua' },
        { kurinKey: 'a', number: 5, kraiCode: 'ua' }
      ]));

      component.ngOnInit();

      expect(component.data.map(k => k.kurinKey)).toEqual(['a', 'b', 'c']);
      expect(component.kraiName('cz')).toBe('Чехія');
      expect(component.kurinCountInKrai('ua')).toBe(2);
    });

    it('offers the краї as choices in the kurin form', () => {
      component.ngOnInit();

      const field = component.managePanelConfig.fields.find(f => f.name === 'kraiCode')!;
      expect(field.type).toBe('select');
      expect(field.options).toEqual([
        { label: 'Чехія', value: 'cz' },
        { label: 'Україна', value: 'ua' }
      ]);
    });

    it('moves the kurin when the admin picks another край', () => {
      component.ngOnInit();
      kurinService.updateKurin.mockReturnValue(of({ kurinKey: '1', number: 101 }));
      kurinService.setKurinKrai.mockReturnValue(of({ kurinKey: '1', number: 101, kraiCode: 'cz' }));
      component.onActionClick({ kurinKey: '1', number: 101, kraiCode: 'ua' }, 'update');

      component.onManageAction({ action: 'update', entity: { kurinKey: '1', number: 101, kraiCode: 'cz' }, entityType: 'kurin' });

      expect(kurinService.setKurinKrai).toHaveBeenCalledWith('1', 'cz');
    });

    it('leaves the край alone when it did not change', () => {
      component.ngOnInit();
      kurinService.updateKurin.mockReturnValue(of({ kurinKey: '1', number: 102 }));
      component.onActionClick({ kurinKey: '1', number: 101, kraiCode: 'ua' }, 'update');

      component.onManageAction({ action: 'update', entity: { kurinKey: '1', number: 102, kraiCode: 'ua' }, entityType: 'kurin' });

      expect(kurinService.setKurinKrai).not.toHaveBeenCalled();
    });
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('initial state before ngOnInit', () => {
    expect(component.data).toEqual([]);
    expect(component.selectedItem).toBeNull();
    expect(component.managePanelVisible).toBe(false);
    expect(component.managePanelParameter).toBe('undef');
  });

  it('should fetch kurins on init', () => {
    component.ngOnInit();
    expect(kurinService.getKurins).toHaveBeenCalledTimes(1);
    expect(component.data).toEqual(mockKurins);
  });

  it('refreshData sets empty array on null response', () => {
    kurinService.getKurins.mockReturnValue(of(null as unknown as KurinDto[]));
    component.refreshData();
    expect(component.data).toEqual([]);
  });

  it('should show no data message when list empty (template)', () => {
    kurinService.getKurins.mockReturnValue(of([]));
    component.refreshData();
    fixture.detectChanges();
    expect(component.data).toHaveLength(0);
    const empty = fixture.nativeElement.querySelector('app-empty-state');
    expect(empty).toBeTruthy();
    expect(empty.textContent).toContain('Куренів ще немає');
  });

  describe('prepareItemActions', () => {
    it('creates Update/Delete menu items', () => {
      component.prepareItemActions(mockKurins[0]);
      expect(component.actions).toHaveLength(2);
      expect(component.actions[0].label).toBe('Редагувати');
      expect(component.actions[1].label).toBe('Видалити');
    });
  });

  describe('onActionClick', () => {
    it('opens panel for update', () => {
      component.onActionClick(mockKurins[0], 'update');
      expect(component.selectedItem).toBe(mockKurins[0]);
      expect(component.managePanelVisible).toBe(true);
      expect(component.managePanelParameter).toBe('update');
    });

    it('opens panel for create (selectedItem null)', () => {
      component.onActionClick(null, 'create');
      expect(component.selectedItem).toBeNull();
      expect(component.managePanelVisible).toBe(true);
      expect(component.managePanelParameter).toBe('create');
    });
  });

  describe('onManageAction', () => {
    beforeEach(() => {
      component.ngOnInit(); // baseline fetch
      kurinService.createKurin.mockReturnValue(of({ kurinKey: '3', number: 103 }));
      kurinService.updateKurin.mockReturnValue(of({ kurinKey: '1', number: 201 }));
      kurinService.deleteKurin.mockReturnValue(of(void 0));
    });

    it('handles create', () => {
      // Creating a kurin creates the kurin — it used to register a manager against a route the
      // API never had, so the admin's "create" did nothing at all.
      const newEntity: KurinDto = { kurinKey: '3', number: 103 };
      component.onManageAction({ action: 'create', entity: newEntity, entityType: 'kurin' });
      expect(kurinService.createKurin).toHaveBeenCalledWith(newEntity);
      expect(kurinService.getKurins).toHaveBeenCalledTimes(2); // initial + refresh
    });

    it('invites the Звʼязковий instead of opening an empty kurin when an address is given', () => {
      kurinService.inviteKurinFounder.mockReturnValue(of({ kurinKey: '3', userKey: 'u', email: 'f@example.com', invitationSent: true }));
      const add = vi.spyOn(TestBed.inject(MessageService), 'add');

      component.onManageAction({
        action: 'create',
        entity: { kurinKey: '', number: 103, kraiCode: 'cz', founderEmail: ' f@example.com ', founderFirstName: 'Ореста', founderLastName: 'Коваль' } as KurinDto,
        entityType: 'kurin'
      });

      expect(kurinService.inviteKurinFounder).toHaveBeenCalledWith({
        email: 'f@example.com', firstName: 'Ореста', lastName: 'Коваль', kurinNumber: 103, kraiCode: 'cz'
      });
      expect(kurinService.createKurin).not.toHaveBeenCalled();
      expect(add).toHaveBeenCalledWith(expect.objectContaining({ severity: 'success', detail: 'Запрошення надіслано на f@example.com.' }));
      expect(kurinService.getKurins).toHaveBeenCalledTimes(2);
    });

    it('asks for the name before inviting', () => {
      const add = vi.spyOn(TestBed.inject(MessageService), 'add');

      component.onManageAction({
        action: 'create',
        entity: { kurinKey: '', number: 103, founderEmail: 'f@example.com' } as KurinDto,
        entityType: 'kurin'
      });

      expect(kurinService.inviteKurinFounder).not.toHaveBeenCalled();
      expect(kurinService.createKurin).not.toHaveBeenCalled();
      expect(add).toHaveBeenCalledWith(expect.objectContaining({ severity: 'warn' }));
    });

    it('says when the letter did not go', () => {
      kurinService.inviteKurinFounder.mockReturnValue(of({ kurinKey: '3', userKey: 'u', email: 'f@example.com', invitationSent: false }));
      const add = vi.spyOn(TestBed.inject(MessageService), 'add');

      component.onManageAction({
        action: 'create',
        entity: { kurinKey: '', number: 103, founderEmail: 'f@example.com', founderFirstName: 'О', founderLastName: 'К' } as KurinDto,
        entityType: 'kurin'
      });

      expect(add).toHaveBeenCalledWith(expect.objectContaining({ severity: 'warn', summary: 'Курінь створено, лист не пішов' }));
    });

    it('names a taken address', () => {
      kurinService.inviteKurinFounder.mockReturnValue(throwError(() =>
        new HttpErrorResponse({ status: 409, error: { error: 'EmailTaken', message: '' } })));
      const add = vi.spyOn(TestBed.inject(MessageService), 'add');

      component.onManageAction({
        action: 'create',
        entity: { kurinKey: '', number: 103, founderEmail: 'f@example.com', founderFirstName: 'О', founderLastName: 'К' } as KurinDto,
        entityType: 'kurin'
      });

      expect(add).toHaveBeenCalledWith(expect.objectContaining({ severity: 'error', detail: 'За цією адресою вже є акаунт.' }));
    });

    it('handles update', () => {
      const updated: KurinDto = { kurinKey: '1', number: 201 };
      component.onManageAction({ action: 'update', entity: updated, entityType: 'kurin' });
      expect(kurinService.updateKurin).toHaveBeenCalledWith(updated);
      expect(kurinService.getKurins).toHaveBeenCalledTimes(2);
    });

    it('handles delete', () => {
      const toDelete: KurinDto = { kurinKey: '1', number: 101 };
      component.onManageAction({ action: 'delete', entity: toDelete, entityType: 'kurin' });
      expect(kurinService.deleteKurin).toHaveBeenCalledWith('1');
      expect(kurinService.getKurins).toHaveBeenCalledTimes(2);
    });
  });

  describe('onOpenClick', () => {
    it('scopes the admin into the kurin before navigating', () => {
      const router = TestBed.inject(Router);
      const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
      authService.setKurinScope.mockReturnValue(of(scopedState));

      component.onOpenClick('1');

      expect(authService.setKurinScope).toHaveBeenCalledWith('1');
      expect(navigate).toHaveBeenCalledWith(['/kurin']);
    });

    it('stays on the panel when scoping fails', () => {
      const router = TestBed.inject(Router);
      const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
      authService.setKurinScope.mockReturnValue(throwError(() => new Error('nope')) as never);

      component.onOpenClick('1');

      expect(navigate).not.toHaveBeenCalled();
    });
  });

  describe('managePanelConfig', () => {
    it('has correct field rules (kurinKey hidden on create)', () => {
      const kurinKeyField = component.managePanelConfig.fields.find(f => f.name === 'kurinKey')!;
      expect(kurinKeyField.hiddenOn).toContain('create');
      expect(kurinKeyField.disabledOn).toContain('update');
    });
  });
});
