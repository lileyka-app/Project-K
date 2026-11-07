import {
  ApplicationConfig,
  inject,
  isDevMode,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideServiceWorker } from '@angular/service-worker';
import { Capacitor } from '@capacitor/core';
import { PreloadAllModules, provideRouter, withPreloading } from '@angular/router';
import { isPlatform, provideIonicAngular } from '@ionic/angular';
import { iosTransitionAnimation, popoverEnterAnimation, popoverLeaveAnimation } from '@rdlabo/ionic-theme-ios27';
import { routes } from './app.routes';
import { authInterceptor } from './auth/auth.interceptor';
import { AppearanceService } from './features/account/appearance.service';
import { KurinRoleNames } from './features/kurin/kurin-role-names.service';

// Zoneless: Angular 22 is zoneless by default, no zone.js in the bundle.
// Ionic picks the mode from the platform: 'ios' on iPhone, 'md' on Android.
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Light or dark as chosen in «Ще → Вигляд», before the first screen is drawn.
    provideAppInitializer(() => inject(AppearanceService).init()),
    provideAppInitializer(() => void inject(KurinRoleNames)),
    provideIonicAngular(isPlatform('ios') ? glassAnimations() : {}),
    provideRouter(routes, withPreloading(PreloadAllModules)),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    // PWA only: the native shells already ship the app offline, and WKWebView has no service
    // workers for capacitor:// anyway.
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode() && !Capacitor.isNativePlatform(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};

/** iOS 27 page and popover motion wherever a Liquid Glass theme is loaded (see styles.scss). */
function glassAnimations() {
  if (typeof CSS === 'undefined') return {};
  if (!CSS.supports('overflow-anchor: auto') && !CSS.supports('text-wrap: pretty')) return {};
  return { navAnimation: iosTransitionAnimation, popoverEnter: popoverEnterAnimation, popoverLeave: popoverLeaveAnimation };
}
