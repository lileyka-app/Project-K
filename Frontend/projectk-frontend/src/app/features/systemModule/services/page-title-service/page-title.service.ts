import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { Observable, catchError, of } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinService } from '../../../kurinModule/services/kurin-service/kurin.service';
import { GroupService } from '../../../kurinModule/services/group-service/group.service';
import { MemberService } from '../../../kurinModule/services/member-service/member.service';
import {
  TitleContextType,
  composePageTitle,
  formatGroupTitle,
  formatKurinTitle,
  formatMemberTitle
} from '../page-title.format';

@Injectable({
  providedIn: 'root'
})
export class PageTitleService {
  // Router is deliberately not injected here: it builds the TitleStrategy that owns this
  // service, so depending on it would close a DI cycle (NG0200) and fail app bootstrap.
  private readonly title = inject(Title);
  private readonly authService = inject(AuthService);
  private readonly kurinService = inject(KurinService);
  private readonly groupService = inject(GroupService);
  private readonly memberService = inject(MemberService);

  private routeTitle: string | null = null;
  private context: string | null = null;
  private navigationToken = 0;

  applyRouteState(routeTitle: string | null, state: RouterStateSnapshot): void {
    this.context = null;
    this.navigationToken++;
    this.routeTitle = routeTitle;
    this.render();
    this.resolveContext(state);
  }

  setContext(context: string | null): void {
    this.context = context;
    this.render();
  }

  private render(): void {
    this.title.setTitle(composePageTitle(this.context ?? this.routeTitle, environment.appName));
  }

  private resolveContext(state: RouterStateSnapshot): void {
    let route: ActivatedRouteSnapshot = state.root;
    const params: Record<string, string> = {};
    let contextType: TitleContextType | null = null;

    while (route) {
      Object.assign(params, route.params);
      const routeContext = route.data['titleContext'];
      if (routeContext === 'kurin' || routeContext === 'group' || routeContext === 'member') {
        contextType = routeContext;
      }
      if (!route.firstChild) {
        break;
      }
      route = route.firstChild;
    }

    if (!contextType) {
      return;
    }

    const token = this.navigationToken;

    switch (contextType) {
      case 'kurin': {
        const kurinKey = params['kurinKey'] ?? this.authService.getAuthStateValue()?.kurinKey;
        if (kurinKey) {
          this.publish(token, this.kurinService.getByKey(kurinKey), kurin => formatKurinTitle(kurin.number, kurin.branch, kurin.namedAfter));
        }
        break;
      }
      case 'group': {
        const groupKey = params['groupKey'];
        if (groupKey) {
          this.publish(token, this.groupService.getByKey(groupKey), group => formatGroupTitle(group.name));
        }
        break;
      }
      case 'member': {
        const memberKey = params['memberKey'];
        if (memberKey) {
          this.publish(token, this.memberService.getByKey(memberKey), member =>
            formatMemberTitle(member.lastName, member.firstName)
          );
        }
        break;
      }
    }
  }

  private publish<T>(
    token: number,
    source: Observable<T>,
    format: (entity: T) => string | null
  ): void {
    source
      .pipe(catchError(() => of(null)))
      .subscribe(entity => {
        if (entity === null || token !== this.navigationToken) {
          return;
        }
        this.setContext(format(entity));
      });
  }
}
