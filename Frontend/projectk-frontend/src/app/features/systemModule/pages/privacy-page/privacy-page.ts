import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';

/** One row of «які дані і навіщо»: what is stored, why, and who puts it there. */
export interface PersonalDataRow {
  what: string;
  why: string;
  who: string;
}

/**
 * Privacy policy of the cloud Лілейка. Public, no sign-in: linked from the welcome page, the join
 * form and «Про Лілейку». The same text lives in docs/user/privacy.md for the site; when one
 * changes, the other follows, and `revisedOn` moves with it.
 */
@Component({
  selector: 'app-privacy-page',
  imports: [RouterLink, ButtonModule],
  templateUrl: './privacy-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './privacy-page.css'
})
export class PrivacyPageComponent {
  readonly revisedOn = '20 жовтня 2026';

  readonly links = {
    site: 'https://rostyslav-mukha.dev',
    github: 'https://github.com/lileyka-app/Project-K',
    security: 'https://github.com/lileyka-app/Project-K/security/policy'
  };

  readonly dataRows: PersonalDataRow[] = [
    { what: 'Прізвище, імʼя, по батькові, дата народження', why: 'реєстр куреня, вік для проб і ступенів', who: 'провід куреня або імпорт із таблиці' },
    { what: 'Пошта і телефон', why: 'вхід, запрошення, відновлення пароля, звʼязок у курені', who: 'провід куреня; сама людина може змінити' },
    { what: 'Адреса, школа', why: 'звʼязок з людиною й облік у курені, якщо край їх веде', who: 'провід куреня або сама людина' },
    { what: 'Контакти батьків: імена й телефони', why: 'звʼязок з батьками юнака', who: 'провід куреня або сама людина' },
    { what: 'Додаткові поля краю', why: 'облік, якого вимагає край куреня', who: 'провід куреня або сама людина' },
    { what: 'Фото', why: 'картка учасника, сильветка гуртка', who: 'провід куреня або сама людина' },
    { what: 'Ступінь, історія ступенів, діловодства', why: 'реєстр, права в системі', who: 'провід куреня' },
    { what: 'Проба, вмілості, журнал підписів', why: 'складання проб і здобуття вмілостей; хто й коли підписав', who: 'впорядник, провід' },
    { what: 'Відзначення і перестороги', why: 'облік у курені', who: 'впорядник, провід' },
    { what: 'Вкладка: нарахування, внески, пільги з підставою, примітки', why: 'облік членських внесків', who: 'скарбники, впорядник, провід' },
    { what: 'Точкування: присутність на подіях, бали з причиною', why: 'змагання гуртків; приватні бали КВ', who: 'провід гуртка й куреня, КВ' },
    { what: 'Відповіді на події', why: 'хто прийде, черга на місця', who: 'сама людина' },
    { what: 'Членства: курені й гуртки, де людина стояла', why: 'історія членства', who: 'система' },
    { what: 'Заявка куреня: імʼя, пошта, телефон, станиця, край, число куреня', why: 'розгляд заявки адміністратором', who: 'Звʼязковий' },
    { what: 'Пароль (хеш), двофакторний вхід, резервні коди (хешовані)', why: 'безпека акаунта', who: 'сама людина' },
    { what: 'Сесії: пристрій, час входу, IP-адреса', why: 'окремий вихід з кожного пристрою, захист від підбору пароля', who: 'система' },
    { what: 'Сповіщення в застосунку', why: 'що сталося і що чекає на тебе', who: 'система' },
    { what: 'Підписка на сповіщення на телефон: адреса push-сервісу, ключі, назва браузера', why: 'доставити сповіщення на телефон', who: 'сама людина, вмикаючи їх' },
    { what: 'Повідомлення про проблему: текст, скриншот, сторінка, версія, роль, ідентифікатор акаунта, браузер', why: 'виправити збій', who: 'сама людина' },
    { what: 'Технічні логи запитів', why: 'пошук збоїв і атак', who: 'система' }
  ];
}
