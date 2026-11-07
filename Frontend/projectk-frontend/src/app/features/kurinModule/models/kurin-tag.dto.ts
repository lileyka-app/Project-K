/** Тег куреня. `memberCount` приходить лише тому, хто може керувати тегами. */
export interface KurinTagDto {
  kurinTagKey: string;
  name: string;
  colorHex: string;
  memberCount?: number | null;
}

/** Теги куреня так, як їх може вживати той, хто дивиться. Вимкнені теги — порожній список. */
export interface KurinTagsDto {
  enabled: boolean;
  canManage: boolean;
  maxPerMember: number;
  palette: string[];
  tags: KurinTagDto[];
}

/** Перемикачі тегів у налаштуваннях куреня. */
export interface KurinTagSettingsDto {
  enabled: boolean;
  /** Що курінь мав би, якби нічого не обирав: у старшому курені теги ввімкнені. */
  enabledByDefault: boolean;
  ownTagsVisible: boolean;
}

export const NO_KURIN_TAGS: KurinTagsDto = { enabled: false, canManage: false, maxPerMember: 10, palette: [], tags: [] };

/**
 * Назви кольорів палітри — для читалки й підказки. Сама палітра приходить із сервера
 * (`KurinTagRules.Palette`), це та сама закрита палітра, що й у груп подій (`BRANDBOOK.md` §2).
 */
export const TAG_COLOR_NAMES: Record<string, string> = {
  '#2F855A': 'Зелений',
  '#2B6CB0': 'Синій',
  '#B7791F': 'Охра',
  '#9B2C2C': 'Вишневий',
  '#6B46C1': 'Фіолетовий',
  '#0E7490': 'Бірюзовий',
  '#4A5568': 'Сірий'
};

/** Коди помилок тегів → що сказати людині. */
export const KURIN_TAG_ERROR_TEXT: Record<string, string> = {
  TagNameTaken: 'Тег із такою назвою в курені вже є.',
  TagsDisabled: 'Теги в курені вимкнені. Їх вмикають у налаштуваннях куреня.',
  TooManyTags: 'У людини не може бути більше 10 тегів.',
  NotInKurin: 'Тег можна поставити лише тому, хто зараз у курені.',
  TagNotFound: 'Цього тегу вже немає. Онови сторінку.',
  TagColorNotInPalette: 'Обери колір із палітри.',
  TagsChangedMeanwhile: 'Теги щойно змінив хтось інший. Онови сторінку й спробуй ще раз.'
};
