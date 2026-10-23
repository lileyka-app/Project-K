import { unsavedChangesGuard } from './unsaved-changes.guard';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';

describe('unsavedChangesGuard', () => {
  const route = {} as ActivatedRouteSnapshot;
  const state = {} as RouterStateSnapshot;

  it('asks the page', () => {
    expect(unsavedChangesGuard({ confirmLeave: () => false }, route, state, state)).toBe(false);
    expect(unsavedChangesGuard({ confirmLeave: () => true }, route, state, state)).toBe(true);
  });

  it('lets go when there is no page', () => {
    expect(unsavedChangesGuard(null as never, route, state, state)).toBe(true);
  });
});
