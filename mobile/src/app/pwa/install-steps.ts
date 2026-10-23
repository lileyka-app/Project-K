import { InstallGuide } from './install-guide';
import { InstallArtName } from './install-art';

/** One numbered step; the system's own labels go in «…» and come out bold. */
export interface InstallStep {
  text: string;
  art?: InstallArtName;
}

/** What /install shows when there is no one-tap button: the way by hand for this browser. */
export interface ManualInstall {
  title: string;
  intro?: string;
  steps: InstallStep[];
  notes: string[];
  /** The page's address to paste into another browser. */
  copyLink: boolean;
  /** Android: an intent:// link that hands the page to Chrome out of an app's own browser. */
  openInChrome: boolean;
}

const SHEET: InstallStep = {
  text: 'У меню, що відкрилося, прогорни вниз і вибери «На початковий екран».',
  art: 'ios-sheet',
};
const ADD: InstallStep = {
  text: 'Натисни «Додати» вгорі праворуч. Іконка Лілейки зʼявиться на екрані.',
  art: 'ios-add',
};
/** iOS keeps a home-screen app's storage apart from Safari's, so the sign-in does not carry over. */
const IOS_SIGN_IN =
  'Відкрий Лілейку з нової іконки й увійди ще раз: застосунок на екрані не бачить входу з браузера.';
const IOS_PUSH =
  'Сповіщення на iPhone приходять лише в Лілейку з початкового екрана (iOS 16.4 і новіші).';

export function manualInstall(guide: Exclude<InstallGuide, { kind: 'installed' }>): ManualInstall {
  switch (guide.kind) {
    case 'in-app': {
      const browser = guide.os === 'ios' ? 'Safari' : 'Chrome';
      const where = guide.app
        ? `вбудованому браузері ${guide.app}`
        : 'вбудованому браузері застосунку';
      return {
        title: `Відкрий у ${browser}`,
        intro: `Ця сторінка відкрилася у ${where}. Звідти встановити Лілейку не вийде — потрібен ${browser}.`,
        steps: [
          {
            text: 'Натисни «⋯» або «⋮» угорі праворуч і вибери «Відкрити в браузері» (буває «Відкрити в Safari» чи «Відкрити в Chrome»).',
            art: 'in-app-menu',
          },
          {
            text: `Якщо такого пункту немає, скопіюй посилання кнопкою нижче й встав його в адресний рядок ${browser}.`,
          },
        ],
        notes: [],
        copyLink: true,
        openInChrome: guide.os === 'android',
      };
    }
    case 'ios':
      return iosSteps(guide);
    case 'android':
      return androidSteps(guide.browser);
    case 'desktop':
      return desktopSteps(guide.browser, guide.mac);
  }
}

function iosSteps(guide: Extract<InstallGuide, { kind: 'ios' }>): ManualInstall {
  const notes = [IOS_SIGN_IN, IOS_PUSH];
  if (guide.tooOld || guide.browser === 'other') {
    return {
      title: 'Відкрий у Safari',
      intro: guide.tooOld
        ? 'На цій версії iOS додати сайт на початковий екран можна лише з Safari.'
        : 'Додати Лілейку на початковий екран найпростіше з Safari.',
      steps: [
        { text: 'Скопіюй посилання кнопкою нижче, відкрий Safari і встав його в адресний рядок.' },
      ],
      notes: [],
      copyLink: true,
      openInChrome: false,
    };
  }
  const where = guide.device === 'iPad' ? 'на iPad' : 'на iPhone';
  if (guide.browser === 'safari') {
    let first: InstallStep;
    if (guide.safari26) {
      first = { text: 'Натисни «⋯» праворуч від адреси внизу, а тоді «Поділитися».', art: 'ios-more' };
    } else if (guide.device === 'iPad') {
      first = { text: 'Натисни «Поділитися» вгорі праворуч, поруч з адресою.', art: 'ios-share' };
    } else {
      first = { text: 'Натисни «Поділитися» в нижній панелі Safari.', art: 'ios-share' };
    }
    const other = guide.safari26
      ? 'На старших iOS кнопка «Поділитися» стоїть просто в панелі Safari.'
      : 'На iOS 26 кнопка «Поділитися» ховається за «⋯» праворуч від адреси.';
    return {
      title: `Додай Лілейку ${where}`,
      steps: [first, SHEET, ADD],
      notes: [other, ...notes],
      copyLink: false,
      openInChrome: false,
    };
  }
  const first: InstallStep =
    guide.browser === 'chrome'
      ? { text: 'Натисни «Поділитися» праворуч в адресному рядку Chrome.', art: 'ios-chrome-share' }
      : {
          text: `Відкрий меню (${guide.browser === 'firefox' ? '«☰» у Firefox' : '«⋯» в Edge'}) і вибери «Поділитися».`,
          art: 'ios-more',
        };
  return {
    title: `Додай Лілейку ${where}`,
    steps: [first, SHEET, ADD],
    notes,
    copyLink: false,
    openInChrome: false,
  };
}

function androidSteps(
  browser: Extract<InstallGuide, { kind: 'android' }>['browser'],
): ManualInstall {
  const installed = 'Якщо в меню є «Відкрити в застосунку» — Лілейку вже встановлено.';
  if (browser === 'samsung') {
    return {
      title: 'Додай Лілейку на телефон',
      steps: [
        {
          text: 'Натисни «≡» унизу праворуч і вибери «Додати сторінку до», а тоді «Головний екран».',
        },
        { text: 'Підтвердь: «Додати».' },
      ],
      notes: [installed],
      copyLink: false,
      openInChrome: false,
    };
  }
  const menu = browser === 'firefox' ? '«⋮» праворуч від адреси' : '«⋮» угорі праворуч';
  const item = browser === 'firefox' ? '«Встановити»' : '«Встановити застосунок»';
  return {
    title: 'Додай Лілейку на телефон',
    steps: [
      {
        text: `Натисни ${menu} і вибери ${item}. Пункт може називатися «Додати на головний екран».`,
        art: 'android-menu',
      },
      {
        text: 'Підтвердь: «Встановити». Іконка Лілейки зʼявиться серед застосунків.',
        art: 'android-install',
      },
    ],
    notes: [installed],
    copyLink: false,
    openInChrome: false,
  };
}

function desktopSteps(
  browser: Extract<InstallGuide, { kind: 'desktop' }>['browser'],
  mac: boolean,
): ManualInstall {
  const phone =
    'Лілейка зроблена під телефон: відкрий цю сторінку там, щоб поставити її на головний екран.';
  if (browser === 'chrome' || browser === 'edge') {
    const menu =
      browser === 'chrome'
        ? 'Значка немає? Пошукай у меню «⋮» пункт «Встановити…» (у розділі збереження й надсилання).'
        : 'Значка немає? Меню «⋯» → «Програми» → «Інсталювати цей сайт як програму».';
    return {
      title: 'Встанови Лілейку на компʼютер',
      steps: [
        {
          text: 'Натисни значок встановлення праворуч в адресному рядку (монітор зі стрілкою).',
          art: 'desktop-chrome',
        },
        { text: 'Підтвердь: «Встановити». Лілейка відкриватиметься в окремому вікні.' },
      ],
      notes: [menu, phone],
      copyLink: false,
      openInChrome: false,
    };
  }
  if (browser === 'safari' && mac) {
    return {
      title: 'Додай Лілейку в Dock',
      steps: [
        { text: 'У меню «Файл» вибери «Додати в Dock…».', art: 'macos-dock' },
        { text: 'Натисни «Додати». Лілейка відкриватиметься з Dock в окремому вікні.' },
      ],
      notes: ['Потрібна macOS Sonoma (14) або новіша.', phone],
      copyLink: false,
      openInChrome: false,
    };
  }
  return {
    title: 'Відкрий у Chrome або Edge',
    intro: 'Цей браузер не вміє встановлювати вебзастосунки.',
    steps: [
      {
        text: 'Скопіюй посилання кнопкою нижче й відкрий його в Chrome чи Edge — або на телефоні.',
      },
    ],
    notes: [phone],
    copyLink: true,
    openInChrome: false,
  };
}
