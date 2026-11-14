/** What a line of the person's own life story is about. */
export type LifeEntryKind = 'Training' | 'Camp' | 'Event';

/** The вишколи the life story knows by name; anything else is «Other», named by the person. */
export type TrainingCourse = 'Kvdch' | 'Kvv' | 'Rov' | 'Kvz' | 'LisovaShkola' | 'ShkolaBulavnykh' | 'Other';

export interface LifeEntryDto {
  memberLifeEntryKey: string;
  kind: LifeEntryKind;
  course: TrainingCourse | null;
  title: string | null;
  /** The course's own running number: the 12th КВЗ is 12. */
  number: number | null;
  startDate: string;
  endDate: string | null;
  place: string | null;
  commandant: string | null;
  role: string | null;
}

export type SaveLifeEntryRequest = Omit<LifeEntryDto, 'memberLifeEntryKey'>;

/** The fixed list by the acronyms everyone in Plast uses; the server names them the same in the PDF. */
export const TRAINING_COURSES: { value: TrainingCourse; label: string }[] = [
  { value: 'LisovaShkola', label: 'ЛШ' },
  { value: 'ShkolaBulavnykh', label: 'ШБ' },
  { value: 'Kvz', label: 'КВЗ' },
  { value: 'Kvv', label: 'КВВ' },
  { value: 'Rov', label: 'РОВ' },
  { value: 'Kvdch', label: 'КВДЧ' },
  { value: 'Other', label: 'Інший' }
];

const COURSE_SHORT: Record<TrainingCourse, string> = {
  Kvdch: 'КВДЧ', Kvv: 'КВВ', Rov: 'РОВ', Kvz: 'КВЗ', LisovaShkola: 'ЛШ', ShkolaBulavnykh: 'ШБ', Other: 'Вишкіл'
};

/** «12-й КВЗ», «ЛШ», or the name the person gave a course the list does not know. */
export function lifeEntryTitle(entry: Pick<LifeEntryDto, 'kind' | 'course' | 'title' | 'number'>): string {
  if (entry.kind !== 'Training') {
    return entry.title ?? '';
  }

  const name = entry.course && entry.course !== 'Other' ? COURSE_SHORT[entry.course] : entry.title || COURSE_SHORT.Other;
  return entry.number ? `${entry.number}-й ${name}` : name;
}
