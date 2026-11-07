import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, convertToParamMap, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable, firstValueFrom, isObservable, of, throwError } from 'rxjs';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinFeaturesService } from '../../services/kurin-features-service/kurin-features.service';
import { KurinBranch } from '../../models/enums/kurin-branch.enum';
import { formerMembersRedirect, legacyArchiveRedirect, RegistryMode, registryModeGuard, RegistryModes, toRegistryMode } from './registry-modes';

describe('RegistryModes', () => {
  const zvyazkovyi = ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'KurinArchive:Read:KurinWide', 'Kurin:Update:KurinWide'];
  const hurtkovyi = ['Group:Update:OwnGroups', 'KurinArchive:Read:OwnGroups'];

  let state: { kurinKey: string | null; isAdmin: boolean; permissions: string[] };
  const branch = signal<KurinBranch | null>(KurinBranch.UPYu);
  let setKurinScope: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    state = { kurinKey: 'k1', isAdmin: false, permissions: [] };
    branch.set(KurinBranch.UPYu);
    setKurinScope = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { getAuthStateValue: () => state, setKurinScope } },
        { provide: KurinFeaturesService, useValue: { branch } }
      ]
    });
  });

  const modes = () => TestBed.inject(RegistryModes).available().map(link => link.mode);

  it('gives the Звʼязковий every mode', () => {
    state.permissions = zvyazkovyi;
    expect(modes()).toEqual(['roster', 'transfers', 'archive', 'import']);
  });

  it('gives an administrator acting in the kurin every mode, as its Звʼязковий', () => {
    state.isAdmin = true;
    expect(modes()).toEqual(['roster', 'transfers', 'archive', 'import']);
  });

  it('gives a гурток\'s провід the склад and the archive of their гурток, nothing that changes the kurin\'s склад', () => {
    state.permissions = hurtkovyi;
    expect(modes()).toEqual(['roster', 'archive']);
  });

  it('gives a member no mode at all', () => {
    state.permissions = ['Member:Read:Own'];
    expect(modes()).toEqual([]);
  });

  it('has no import in a senior kurin, which has no гуртки to put people in', () => {
    state.permissions = zvyazkovyi;
    branch.set(KurinBranch.USP);
    expect(modes()).toEqual(['roster', 'transfers', 'archive']);
  });

  describe('guards', () => {
    const run = (guard: ReturnType<typeof registryModeGuard>, route: Partial<ActivatedRouteSnapshot> = {}) =>
      TestBed.runInInjectionContext(() => guard(route as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
    const url = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);
    const settle = async (result: unknown) => isObservable(result) ? firstValueFrom(result as Observable<unknown>) : result;

    it('sends someone without the mode to a mode they have, or refuses them', () => {
      state.permissions = hurtkovyi;
      expect(run(registryModeGuard('archive'))).toBe(true);
      expect(url(run(registryModeGuard('transfers')))).toBe('/kurin/registry');

      state.permissions = [];
      expect(url(run(registryModeGuard('roster')))).toBe('/forbidden');
    });

    it('sends the old addresses on to their mode with their query, the QR\'s code included', () => {
      const redirect = (mode: RegistryMode, queryParams: Record<string, string>) => TestBed.runInInjectionContext(() =>
        toRegistryMode(mode)({ queryParams } as unknown as Parameters<ReturnType<typeof toRegistryMode>>[0]));

      expect(url(redirect('transfers', { code: 'PL-4F7A2-9D4A1' }))).toBe('/kurin/registry/transfers?code=PL-4F7A2-9D4A1');
      expect(url(redirect('import', {}))).toBe('/kurin/registry/import');
    });

    it('sends the old «former members» address of the склад to the archive', () => {
      const route = { queryParamMap: convertToParamMap({ former: '1' }) };
      expect(url(run(formerMembersRedirect, route))).toBe('/kurin/registry/archive?former=1');
      expect(run(formerMembersRedirect, { queryParamMap: convertToParamMap({}) })).toBe(true);
    });

    it('opens the archive of the kurin acted in from its old address', async () => {
      const result = await settle(run(legacyArchiveRedirect, { paramMap: convertToParamMap({ kurinKey: 'k1' }) }));
      expect(url(result)).toBe('/kurin/registry/archive');
      expect(setKurinScope).not.toHaveBeenCalled();
    });

    it('switches to another kurin from its old archive address, and refuses where the person does not stand', async () => {
      setKurinScope.mockReturnValue(of(state));
      const switched = await settle(run(legacyArchiveRedirect, { paramMap: convertToParamMap({ kurinKey: 'k2' }) }));
      expect(setKurinScope).toHaveBeenCalledWith('k2');
      expect(url(switched)).toBe('/kurin/registry/archive');

      setKurinScope.mockReturnValue(throwError(() => new Error('not here')));
      const refused = await settle(run(legacyArchiveRedirect, { paramMap: convertToParamMap({ kurinKey: 'k3' }) }));
      expect(url(refused)).toBe('/forbidden');
    });
  });
});
