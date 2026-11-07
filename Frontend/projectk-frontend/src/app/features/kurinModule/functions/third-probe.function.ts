import {
  ProbeProjectArea,
  ProbeProjectReach,
  ThirdProbeCampKind,
  SetThirdProbeStagesRequestDto,
  ThirdProbeDto
} from '../models/probes-and-badges/probe-project.dto';

/** The areas in the programme's order, with the letters the programme gives them. */
export const PROJECT_AREAS: readonly { value: ProbeProjectArea; letter: string; label: string }[] = [
  { value: 'OathAndIdea', letter: 'А', label: 'Пластова присяга, ідея та організація' },
  { value: 'Community', letter: 'Б', label: 'Громадська діяльність' },
  { value: 'PlastActivities', letter: 'В', label: 'Пластові заняття' },
  { value: 'Nature', letter: 'Г', label: 'Життя в природі' },
  { value: 'Leadership', letter: 'Ґ', label: 'Провідництво' }
];

export const PROJECT_REACHES: readonly { value: ProbeProjectReach; label: string }[] = [
  { value: 'GroupOrKurin', label: 'гурток або курінь' },
  { value: 'Stanytsia', label: 'станиця' },
  { value: 'UkrainianCommunity', label: 'українська громада' },
  { value: 'WiderSociety', label: 'ширша громада' }
];

export const CAMP_KINDS: readonly { value: ThirdProbeCampKind; label: string }[] = [
  { value: 'Kvt', label: 'КВТ' },
  { value: 'Alternative', label: 'Альтернативний табір' }
];

export function areaOf(value: ProbeProjectArea | null): { letter: string; label: string } | null {
  return PROJECT_AREAS.find(area => area.value === value) ?? null;
}

export function reachLabels(reach: ProbeProjectReach[]): string[] {
  return PROJECT_REACHES.filter(item => reach.includes(item.value)).map(item => item.label);
}

export function campKindLabel(kind: ThirdProbeCampKind | null): string | null {
  return CAMP_KINDS.find(item => item.value === kind)?.label ?? null;
}

/**
 * The day youth ends: the 18th birthday. A person born on 29 February gets 1 March in a year
 * without one, the way the calendar rolls it.
 */
export function youthEndsOn(dateOfBirth: Date | string | null, age: number): Date | null {
  if (!dateOfBirth) {
    return null;
  }
  const born = new Date(dateOfBirth);
  if (Number.isNaN(born.getTime())) {
    return null;
  }
  return new Date(born.getFullYear() + age, born.getMonth(), born.getDate());
}

export interface CountdownParts {
  days: number;
  hours: number;
  minutes: number;
  seconds: number;
  /** The deadline has passed. */
  isOver: boolean;
}

/** What is left until `deadline`, cut into the units the countdown shows. */
export function countdownParts(deadline: Date, now: Date): CountdownParts {
  const left = Math.max(0, deadline.getTime() - now.getTime());
  const seconds = Math.floor(left / 1000);
  return {
    days: Math.floor(seconds / 86_400),
    hours: Math.floor(seconds / 3_600) % 24,
    minutes: Math.floor(seconds / 60) % 60,
    seconds: seconds % 60,
    isOver: left === 0
  };
}

/** How urgent the countdown looks: calm, the last three months, the last month. */
export function countdownMood(parts: CountdownParts): 'calm' | 'soon' | 'urgent' | 'over' {
  if (parts.isOver) {
    return 'over';
  }
  if (parts.days < 30) {
    return 'urgent';
  }
  return parts.days < 90 ? 'soon' : 'calm';
}

export interface ChecklistItem {
  key: 'reflection' | 'projects' | 'camp' | 'badges' | 'resume';
  label: string;
  detail: string;
  done: boolean;
}

/**
 * The probe's requirements, each as done or not, with what is still missing. Read straight from
 * the tab: nothing here is stored.
 */
export function thirdProbeChecklist(tab: ThirdProbeDto): ChecklistItem[] {
  const file = tab.file;
  const limits = tab.limits;
  const mains = tab.projects.filter(project => !project.isSpare);
  const doneProjects = mains.filter(project => project.completedAtUtc).length;
  const areas = new Set(mains.map(project => project.area).filter(Boolean));
  const duringProbe = tab.badges.filter(badge => badge.duringProbe);
  const physical = duringProbe.some(badge => badge.badgeId === file?.physicalBadgeId);

  return [
    {
      key: 'reflection',
      label: 'Рефлексія зі звʼязковим',
      detail: confirmationDetail(file?.reflectionConfirmedAtUtc, !!file?.reflectionOn, 'підтверджено', 'ще не відбулася'),
      done: !!file?.reflectionConfirmedAtUtc
    },
    {
      key: 'projects',
      label: 'Проєкти',
      detail: `виконано ${doneProjects} з ${limits.maxMainProjects} · ділянок ${areas.size} з ${PROJECT_AREAS.length}`,
      done: doneProjects >= limits.maxMainProjects && areas.size === PROJECT_AREAS.length
    },
    {
      key: 'camp',
      label: 'Табір',
      detail: file?.campName ? 'додано' : 'ще не вказано',
      done: !!file?.campName
    },
    {
      key: 'badges',
      label: 'Вмілості',
      detail: `${Math.min(duringProbe.length, limits.requiredBadges)} з ${limits.requiredBadges}` + (physical ? ' · є тіловиховання' : ' · без тіловиховання'),
      done: duringProbe.length >= limits.requiredBadges && physical
    },
    {
      key: 'resume',
      label: 'Пластове резюме',
      detail: file?.resume.trim() ? 'написано' : 'ще не написано',
      done: !!file?.resume.trim()
    }
  ];
}

/** Where a part leaders confirm stands: confirmed, written down and waiting, or not there yet. */
function confirmationDetail(confirmedAt: string | null | undefined, written: boolean, confirmed: string, missing: string): string {
  if (confirmedAt) {
    return confirmed;
  }
  return written ? 'чекає підтвердження' : missing;
}

/** A picked day as the API's `yyyy-MM-dd`, in local time so the day does not slip at midnight UTC. */
export function toIsoDate(date: Date | null): string | null {
  if (!date) {
    return null;
  }
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** The API's `yyyy-MM-dd` as a local day for the date picker. */
export function fromIsoDate(value: string | null): Date | null {
  if (!value) {
    return null;
  }
  const [year, month, day] = value.split('-').map(Number);
  return year && month && day ? new Date(year, month - 1, day) : null;
}

/** A day as people read it: «1 листопада 2026». */
export function dayLabel(value: string | null): string | null {
  return fromIsoDate(value)?.toLocaleDateString('uk-UA', { day: 'numeric', month: 'long', year: 'numeric' }) ?? null;
}

/** The referent's stages, in the order a probe goes through them. */
export const THIRD_PROBE_STAGES: readonly { key: keyof SetThirdProbeStagesRequestDto; label: string; hint?: string; closing: boolean }[] = [
  { key: 'motivationLetterOn', label: 'Мотиваційний лист', hint: 'план здачі проби; пише 15-річний', closing: false },
  { key: 'selfReflectionOn', label: 'Саморефлексія', closing: false },
  { key: 'applicationSubmittedOn', label: 'Зголошення до краю', closing: false },
  { key: 'openedOn', label: 'Пробу відкрито', closing: false },
  { key: 'completedOn', label: 'Пробу завершено', closing: true },
  { key: 'namedOn', label: 'Іменовано', closing: true },
  { key: 'archivedOn', label: 'В архіві', closing: true }
];

/** Bytes as people read them: «3,2 МБ». */
export function megabytesLabel(bytes: number): string {
  return (bytes / (1024 * 1024)).toLocaleString('uk-UA', { maximumFractionDigits: 1 }) + ' МБ';
}
