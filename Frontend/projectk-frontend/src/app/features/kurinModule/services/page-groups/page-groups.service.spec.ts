import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { vi } from 'vitest';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { DuesService } from '../../../duesModule/services/dues-service/dues.service';
import { PageGroups } from './page-groups.service';

describe('PageGroups', () => {
  const permissions = {
    canSeeKurinDues: vi.fn(() => true),
    canSeeGroupDues: vi.fn(() => true),
    canReadKurinSettings: vi.fn(() => true)
  };
  const dues = { getReadableGroups: vi.fn(() => of([{ groupKey: 'g1', groupName: 'Соколи' }])) };

  function groups(): PageGroups {
    TestBed.configureTestingModule({
      providers: [
        { provide: PermissionService, useValue: permissions },
        { provide: DuesService, useValue: dues }
      ]
    });
    return TestBed.inject(PageGroups);
  }

  const labels = (tabs: { label: string }[]) => tabs.map(tab => tab.label);

  beforeEach(() => {
    permissions.canSeeKurinDues.mockReturnValue(true);
    permissions.canSeeGroupDues.mockReturnValue(true);
    permissions.canReadKurinSettings.mockReturnValue(true);
  });

  it('puts the calendar and planning side by side', () => {
    expect(groups().calendar('k1').map(tab => tab.link)).toEqual(['/calendar/k1', '/planning/k1']);
  });

  it('opens the kurin box first, then each гурток the person keeps', async () => {
    const tabs = await firstValueFrom(groups().dues('k1'));

    expect(labels(tabs)).toEqual(['Курінь', 'Соколи']);
    expect(tabs[1].link).toBe('/group/g1/dues');
  });

  it('leaves out the kurin box for whoever does not keep it', async () => {
    permissions.canSeeKurinDues.mockReturnValue(false);

    expect(labels(await firstValueFrom(groups().dues('k1')))).toEqual(['Соколи']);
  });

  it('gives everyone the account settings, and the kurin ones to whoever may read them', () => {
    const pages = groups();
    expect(labels(pages.settings('k1'))).toEqual(['Курінь', 'Акаунт']);

    permissions.canReadKurinSettings.mockReturnValue(false);
    expect(labels(pages.settings('k1'))).toEqual(['Акаунт']);
    expect(labels(pages.settings(null))).toEqual(['Акаунт']);
  });
});
