import { AfterViewInit, Component, inject, signal, ChangeDetectionStrategy, viewChild } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ToolbarHeaderComponent } from "./features/kurinModule/components/toolbar-header/toolbar-header";
import { ColdStartBannerComponent } from './features/systemModule/components/cold-start-banner/cold-start-banner';
import { MfaSetupDialogComponent } from './features/authModule/components/mfa-setup-dialog/mfa-setup-dialog';
import { DevRoleSwitcherComponent } from './features/systemModule/components/dev-role-switcher/dev-role-switcher';
import { MfaEnforcerService } from './features/authModule/services/mfa-enforcer-service/mfa-enforcer.service';
import { ToastModule } from '@openng/optimus-ui/toast';
import { InstallAppBannerComponent } from './features/systemModule/components/install-app-banner/install-app-banner';
import { installBannerRoute } from './features/systemModule/functions/install-app.function';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToolbarHeaderComponent, ColdStartBannerComponent, MfaSetupDialogComponent, DevRoleSwitcherComponent, ToastModule, InstallAppBannerComponent],
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './app.css'
})
export class App implements AfterViewInit {
  protected readonly title = signal('projectk-frontend');
  protected readonly isPublicShellRoute = signal(false);
  /** «Лілейка на телефоні» shows in the signed-in shell and on the login page (it decides the rest). */
  protected readonly isInstallBannerRoute = signal(false);
  private readonly mfaEnforcer = inject(MfaEnforcerService);
  private readonly router = inject(Router);

  readonly mfaDialog = viewChild.required<MfaSetupDialogComponent>('mfaDialog');

  constructor() {
    this.updateShellVisibility(this.router.url);
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(event => this.updateShellVisibility(event.urlAfterRedirects));
  }

  ngAfterViewInit(): void {
    this.mfaEnforcer.checkAndEnforce(this.mfaDialog());
  }

  private updateShellVisibility(url: string): void {
    const path = url.split('?')[0].split('#')[0];
    this.setShellVisibility(path);
    this.isInstallBannerRoute.set(installBannerRoute(path, this.isPublicShellRoute()));
  }

  private setShellVisibility(path: string): void {
    // `/` is the welcome page for a guest and the dashboard for a person with a card: the same
    // address, so the route that matched says which, not the path.
    if (path === '/') {
      this.isPublicShellRoute.set(this.deepestRouteData()['shell'] !== 'app');
      return;
    }
    const publicPrefixes = ['/welcome', '/login', '/join', '/activate', '/v', '/c'];
    this.isPublicShellRoute.set(
      publicPrefixes.some(prefix => path === prefix || path.startsWith(`${prefix}/`))
    );
  }

  private deepestRouteData(): Record<string, unknown> {
    let route = this.router.routerState.snapshot.root;
    while (route.firstChild) {
      route = route.firstChild;
    }
    return route.data;
  }
}
