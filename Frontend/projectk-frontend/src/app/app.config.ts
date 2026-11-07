import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { NavigationEnd, Router, TitleStrategy, provideRouter, withNavigationErrorHandler } from '@angular/router';
import { KurinFeaturesService } from './features/kurinModule/services/kurin-features-service/kurin-features.service';

import { routes } from './app.routes';
import { clearStaleAppShellMarker, recoverFromStaleAppShell } from './features/systemModule/functions/stale-app-shell.function';
import { ProjectKTitleStrategy } from './features/systemModule/services/page-title.strategy';
import { provideOptimus } from '@openng/optimus-ui/config';
import { LileykaPreset } from './lileyka-preset';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { AuthInterceptor } from './features/authModule/services/auth.interceptor';
import { HealthInterceptor } from './features/systemModule/services/health.interceptor';
import { DemoApiInterceptor } from './features/systemModule/services/demo-api.interceptor';
import { RequestFeedbackInterceptor } from './features/systemModule/services/request-feedback.interceptor';
import { UserActionService } from './features/systemModule/services/user-action-service/user-action.service';
import { environment } from '../environments/environment';
import { HealthBannerService } from './features/systemModule/services/health-banner-service/health-banner.service';
import { ThemeService } from './features/systemModule/services/theme-service/theme.service';
import { MessageService } from '@openng/optimus-ui/api';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    // Кожен маршрут тут лінивий, тож після деплою перший же перехід у вже відкритій вкладці просить
    // чанк, якого на сервері більше немає, і клік просто нічого не робить. Ловимо це й ставимо
    // вкладку на свіжу оболонку — один раз, щоб зламаний деплой не перетворився на цикл.
    provideRouter(routes, withNavigationErrorHandler(error => {
      recoverFromStaleAppShell(error);
    })),
    { provide: TitleStrategy, useClass: ProjectKTitleStrategy },
    MessageService,
    provideOptimus({
        translation: {
          firstDayOfWeek: 1,
          dayNames: ['Неділя', 'Понеділок', 'Вівторок', 'Середа', 'Четвер', 'Пʼятниця', 'Субота'],
          dayNamesShort: ['Нед', 'Пон', 'Вів', 'Сер', 'Чет', 'Птн', 'Суб'],
          dayNamesMin: ['Нд', 'Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб'],
          monthNames: ['Січень', 'Лютий', 'Березень', 'Квітень', 'Травень', 'Червень', 'Липень', 'Серпень', 'Вересень', 'Жовтень', 'Листопад', 'Грудень'],
          monthNamesShort: ['Січ', 'Лют', 'Бер', 'Кві', 'Тра', 'Чер', 'Лип', 'Сер', 'Вер', 'Жов', 'Лис', 'Гру'],
          today: 'Сьогодні',
          clear: 'Очистити',
          accept: 'Так',
          reject: 'Ні',
          choose: 'Обрати',
          upload: 'Завантажити',
          cancel: 'Скасувати',
          apply: 'Застосувати',
          emptyMessage: 'Нічого немає',
          emptyFilterMessage: 'Збігів не знайдено',
          emptySearchMessage: 'Збігів не знайдено',
          emptySelectionMessage: 'Нічого не обрано',
          selectionMessage: '{0} обрано',
          searchMessage: 'Знайдено {0}',
          fileChosenMessage: 'Файлів: {0}',
          noFileChosenMessage: 'Файл не обрано',
          passwordPrompt: 'Введіть пароль',
          weak: 'Слабкий',
          medium: 'Середній',
          strong: 'Надійний',
          pending: 'Перевіряємо…',
          chooseYear: 'Оберіть рік',
          chooseMonth: 'Оберіть місяць',
          chooseDate: 'Оберіть дату',
          prevDecade: 'Попереднє десятиліття',
          nextDecade: 'Наступне десятиліття',
          prevYear: 'Попередній рік',
          nextYear: 'Наступний рік',
          prevMonth: 'Попередній місяць',
          nextMonth: 'Наступний місяць',
          prevHour: 'Попередня година',
          nextHour: 'Наступна година',
          prevMinute: 'Попередня хвилина',
          nextMinute: 'Наступна хвилина',
          weekHeader: 'Тиж',
          startsWith: 'Починається з',
          contains: 'Містить',
          notContains: 'Не містить',
          endsWith: 'Закінчується на',
          equals: 'Дорівнює',
          notEquals: 'Не дорівнює',
          noFilter: 'Без фільтра',
          matchAll: 'Усі умови',
          matchAny: 'Будь-яка умова',
          addRule: 'Додати умову',
          removeRule: 'Прибрати умову',
          dateIs: 'Дата дорівнює',
          dateIsNot: 'Дата не дорівнює',
          dateBefore: 'Дата до',
          dateAfter: 'Дата після',
          aria: {
            trueLabel: 'Так',
            falseLabel: 'Ні',
            nullLabel: 'Не обрано',
            star: '1 зірка',
            stars: '{star} зірок',
            selectAll: 'Обрати все',
            unselectAll: 'Зняти вибір',
            close: 'Закрити',
            previous: 'Назад',
            next: 'Далі',
            navigation: 'Навігація',
            scrollTop: 'Догори',
            moveTop: 'На початок',
            moveUp: 'Вище',
            moveDown: 'Нижче',
            moveBottom: 'У кінець',
            moveToTarget: 'Перемістити',
            moveToSource: 'Повернути',
            moveAllToTarget: 'Перемістити все',
            moveAllToSource: 'Повернути все',
            pageLabel: 'Сторінка {page}',
            firstPageLabel: 'Перша сторінка',
            lastPageLabel: 'Остання сторінка',
            nextPageLabel: 'Наступна сторінка',
            prevPageLabel: 'Попередня сторінка',
            rowsPerPageLabel: 'Рядків на сторінці',
            jumpToPageDropdownLabel: 'Перейти до сторінки',
            jumpToPageInputLabel: 'Перейти до сторінки',
            selectRow: 'Обрати рядок',
            unselectRow: 'Зняти вибір рядка',
            expandRow: 'Розгорнути рядок',
            collapseRow: 'Згорнути рядок',
            showFilterMenu: 'Показати фільтр',
            hideFilterMenu: 'Сховати фільтр',
            filterOperator: 'Оператор фільтра',
            filterConstraint: 'Умова фільтра',
            editRow: 'Редагувати рядок',
            saveEdit: 'Зберегти',
            cancelEdit: 'Скасувати',
            listView: 'Список',
            gridView: 'Сітка',
            slide: 'Слайд',
            slideNumber: 'Слайд {slideNumber}',
            zoomImage: 'Збільшити зображення',
            zoomIn: 'Збільшити',
            zoomOut: 'Зменшити',
            rotateRight: 'Повернути праворуч',
            rotateLeft: 'Повернути ліворуч',
            listLabel: 'Список варіантів',
            selectColor: 'Обрати колір',
            browseFiles: 'Обрати файли',
            maximizeLabel: 'Розгорнути'
          }
        },
        // Усі оверлеї (випадайки, календарі, меню) — у body. Всередині діалогу оверлей, доданий
        // «до себе», обрізався контентом діалогу, сам створював прокрутку і від неї ж закривався.
        overlayAppendTo: 'body',
        theme: {
          preset: LileykaPreset,
          options: {
            darkModeSelector: '[data-theme="dark"]',
          }
        }
    }),
    provideHttpClient(withInterceptorsFromDi()),
    // Constructing ThemeService applies the stored preference. Without this it would only
    // happen once the toolbar renders, so login and welcome would ignore a dark preference.
    provideAppInitializer(() => void inject(ThemeService)),
    // Вдала навігація означає, що оболонка жива — знімаємо позначку, щоб наступний деплой знову
    // мав право на одну спробу відновитись.
    provideAppInitializer(() => {
      inject(Router).events.subscribe(event => {
        if (event instanceof NavigationEnd) {
          clearStaleAppShellMarker();
        }
      });
    }),
    provideAppInitializer(() => inject(HealthBannerService).startSessionCheck()),
    provideAppInitializer(() => inject(UserActionService).start()),
    // Reads the current kurin from the start, so its own role names are there on any first page.
    provideAppInitializer(() => void inject(KurinFeaturesService)),
    { provide: HTTP_INTERCEPTORS, useClass: RequestFeedbackInterceptor, multi: true },
    // The static demo has no API: recorded fixtures answer before any other interceptor runs.
    ...(environment.isStaticDemo ? [{ provide: HTTP_INTERCEPTORS, useClass: DemoApiInterceptor, multi: true }] : []),
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: HealthInterceptor, multi: true }
  ]
};
