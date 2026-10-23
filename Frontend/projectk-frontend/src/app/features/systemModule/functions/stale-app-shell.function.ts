import { NavigationError } from '@angular/router';

/**
 * Скільки разів поспіль дозволяємо перезавантажитись через ненайдений чанк. Один — і досить: якщо
 * після свіжого `index.html` чанка все одно немає, то це не застаріла вкладка, а зламаний деплой, і
 * циклічне перезавантаження зробить із нього нескінченний цикл замість помилки.
 */
const RELOAD_MARKER = 'lileyka:shell-reloaded';

/**
 * Браузер каже про це по-різному в різних рушіях, і жоден із варіантів не має коду помилки — тож
 * лишається текст. Порівнюємо в нижньому регістрі й по підрядках, які в усіх спільні.
 */
const CHUNK_FAILURE_NEEDLES = [
  'failed to fetch dynamically imported module',
  'error loading dynamically imported module',
  'importing a module script failed',
  'loading chunk',
  'failed to load module script'
];

function messageOf(error: unknown): string {
  if (typeof error === 'string') {
    return error;
  }

  if (error && typeof error === 'object' && 'message' in error) {
    const { message } = error as { message: unknown };
    return typeof message === 'string' ? message : '';
  }

  return '';
}

/**
 * Чи це той випадок, коли сторінка просить шматок застосунку, якого вже немає на сервері.
 *
 * Так буває після деплою: вкладка тримає стару оболонку з іменами чанків попередньої збірки, а всі
 * маршрути тут ліниві — тож **перший же перехід** мертвий. Для людини це виглядає як зламана кнопка.
 */
export function isStaleAppShellError(error: unknown): boolean {
  const message = messageOf(error).toLowerCase();
  return CHUNK_FAILURE_NEEDLES.some(needle => message.includes(needle));
}

/**
 * Ставить вкладку на нову збірку: `index.html` віддається з `no-cache` (див. `docker/nginx/`), тож
 * звичайне завантаження за тією ж адресою приносить свіжу оболонку з правильними іменами чанків.
 *
 * Повертає `true`, якщо перезавантаження запущено, і `false`, якщо ми це вже пробували — тоді
 * помилку треба показати, а не ховати за ще одним циклом.
 *
 * @param goTo Як саме піти за адресою. Параметром, бо `window.location` у браузері не підмінити,
 *   а без підміни тест на «пробуємо один раз» перезавантажив би сам себе.
 */
export function recoverFromStaleAppShell(
  error: NavigationError,
  goTo: (url: string) => void = url => window.location.assign(url)
): boolean {
  if (!isStaleAppShellError(error.error)) {
    return false;
  }

  if (sessionStorage.getItem(RELOAD_MARKER)) {
    return false;
  }

  sessionStorage.setItem(RELOAD_MARKER, '1');
  goTo(error.url);
  return true;
}

/** Викликається після вдалої навігації: вкладка жива, тож наступна невдача заслуговує на свою спробу. */
export function clearStaleAppShellMarker(): void {
  sessionStorage.removeItem(RELOAD_MARKER);
}
