/**
 * Пластова гілка куреня. Проби й вмілості — юнацькі речі, тож поза УПЮ їх не показуємо.
 */
export enum KurinBranch {
  UPYu = 'UPYu',
  USP = 'USP',
  UPS = 'UPS'
}

export const KURIN_BRANCH_LABELS: Record<KurinBranch, string> = {
  [KurinBranch.UPYu]: 'УПЮ',
  [KurinBranch.USP]: 'УСП',
  [KurinBranch.UPS]: 'УПС'
};

/**
 * Гілка куреня і його число в ній. Старший курінь може бути в двох гілках з різними числами:
 * Орден Хрестоносців — 5-й курінь УСП і 20-й курінь УПС.
 */
export interface KurinNumbering {
  branch: KurinBranch;
  /** Null для підготовчого куреня. */
  number: number | null;
}

/** Чи має ця гілка юнацький вишкіл — проби, вмілості, точкування. */
export function hasYouthProgram(branch: KurinBranch | null | undefined): boolean {
  return (branch ?? KurinBranch.UPYu) === KurinBranch.UPYu;
}

/** Чи є в курені гуртки. У старшому курені їх немає зовсім: людей там розрізняють теги. */
export function hasGroups(branch: KurinBranch | null | undefined): boolean {
  return !isSeniorBranch(branch);
}

/** УСП чи УПС — старші гілки, де курінь відкривають і змінюють через адміністратора. */
export function isSeniorBranch(branch: KurinBranch | null | undefined): boolean {
  return branch === KurinBranch.USP || branch === KurinBranch.UPS;
}
