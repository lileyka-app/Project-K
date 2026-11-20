import { ProbeProjectArea, ProbeProjectReach, ThirdProbeDto, ThirdProbeStagesRequest } from './kurin.models';

/** The areas with the letters the programme gives them (web third-probe.function.ts). */
export const PROJECT_AREAS: readonly { value: ProbeProjectArea; letter: string; label: string }[] = [
  { value: 'OathAndIdea', letter: 'А', label: 'Пластова присяга, ідея та організація' },
  { value: 'Community', letter: 'Б', label: 'Громадська діяльність' },
  { value: 'PlastActivities', letter: 'В', label: 'Пластові заняття' },
  { value: 'Nature', letter: 'Г', label: 'Життя в природі' },
  { value: 'Leadership', letter: 'Ґ', label: 'Провідництво' },
];

const REACH_LABELS: Record<ProbeProjectReach, string> = {
  GroupOrKurin: 'гурток або курінь',
  Stanytsia: 'станиця',
  UkrainianCommunity: 'українська громада',
  WiderSociety: 'ширша громада',
};

export function areaOf(value: ProbeProjectArea | null): { letter: string; label: string } | null {
  return PROJECT_AREAS.find((area) => area.value === value) ?? null;
}

export function reachLabels(reach: ProbeProjectReach[]): string[] {
  return reach.map((value) => REACH_LABELS[value]).filter(Boolean);
}

/** The 18th birthday, when youth ends and the third probe with it. */
export function youthEndsOn(dateOfBirth: string | null | undefined, age: number): Date | null {
  if (!dateOfBirth) return null;
  const born = new Date(dateOfBirth);
  return Number.isNaN(born.getTime()) ? null : new Date(born.getFullYear() + age, born.getMonth(), born.getDate());
}

export interface CountdownParts {
  days: number;
  hours: number;
  minutes: number;
  seconds: number;
  isOver: boolean;
}

export function countdownParts(deadline: Date, now: Date): CountdownParts {
  const left = Math.max(0, deadline.getTime() - now.getTime());
  const seconds = Math.floor(left / 1000);
  return {
    days: Math.floor(seconds / 86_400),
    hours: Math.floor(seconds / 3_600) % 24,
    minutes: Math.floor(seconds / 60) % 60,
    seconds: seconds % 60,
    isOver: left === 0,
  };
}

/** «день», «дні», «днів». */
export function dayWord(days: number): string {
  const lastTwo = days % 100;
  const last = days % 10;
  if (lastTwo >= 11 && lastTwo <= 14) return 'днів';
  if (last === 1) return 'день';
  return last >= 2 && last <= 4 ? 'дні' : 'днів';
}

export interface ChecklistItem {
  label: string;
  detail: string;
  done: boolean;
}

function confirmation(confirmedAt: string | null | undefined, written: boolean, confirmed: string, missing: string): string {
  if (confirmedAt) return confirmed;
  return written ? 'чекає підтвердження' : missing;
}

/** What is done and what is not, read from the tab (web thirdProbeChecklist). */
export function thirdProbeChecklist(tab: ThirdProbeDto): ChecklistItem[] {
  const file = tab.file;
  const mains = tab.projects.filter((project) => !project.isSpare);
  const doneProjects = mains.filter((project) => project.completedAtUtc).length;
  const areas = new Set(mains.map((project) => project.area).filter(Boolean));
  const during = tab.badges.filter((badge) => badge.duringProbe);
  const physical = during.some((badge) => badge.badgeId === file?.physicalBadgeId);
  const required = tab.limits.requiredBadges;
  return [
    {
      label: 'Рефлексія',
      detail: confirmation(file?.reflectionConfirmedAtUtc, !!file?.reflectionOn, 'підтверджено', 'ще не відбулася'),
      done: !!file?.reflectionConfirmedAtUtc,
    },
    {
      label: 'Проєкти',
      detail: `виконано ${doneProjects} з ${tab.limits.maxMainProjects} · ділянок ${areas.size} з ${PROJECT_AREAS.length}`,
      done: doneProjects >= tab.limits.maxMainProjects && areas.size === PROJECT_AREAS.length,
    },
    {
      label: 'Табір',
      detail: file?.campName ? 'додано' : 'ще не вказано',
      done: !!file?.campName,
    },
    {
      label: 'Вмілості',
      detail: `${Math.min(during.length, required)} з ${required}` + (physical ? ' · є тіловиховання' : ' · без тіловиховання'),
      done: during.length >= required && physical,
    },
    {
      label: 'Пластове резюме',
      detail: file?.resume.trim() ? 'написано' : 'ще не написано',
      done: !!file?.resume.trim(),
    },
  ];
}

/** The referent's stages, in the order a probe goes through them (web THIRD_PROBE_STAGES). */
export const THIRD_PROBE_STAGES: readonly { key: keyof ThirdProbeStagesRequest; label: string; hint?: string; closing: boolean }[] = [
  { key: 'motivationLetterOn', label: 'Мотиваційний лист', hint: 'план здачі проби; пише 15-річний', closing: false },
  { key: 'selfReflectionOn', label: 'Саморефлексія', closing: false },
  { key: 'applicationSubmittedOn', label: 'Зголошення до краю', closing: false },
  { key: 'openedOn', label: 'Пробу відкрито', closing: false },
  { key: 'completedOn', label: 'Пробу завершено', closing: true },
  { key: 'namedOn', label: 'Іменовано', closing: true },
  { key: 'archivedOn', label: 'В архіві', closing: true },
];

/**
 * Wraps `start` so that calls made while it is pending share its result instead of starting again:
 * several pictures dropped into an unsaved project make one project, not one each.
 */
export function sharedWhilePending<T>(start: () => Promise<T>): () => Promise<T> {
  let pending: Promise<T> | null = null;
  return () => (pending ??= start().finally(() => (pending = null)));
}
