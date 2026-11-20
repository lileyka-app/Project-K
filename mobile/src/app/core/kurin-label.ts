/**
 * How a kurin is named, as the web's shared/functions/kurin-label.function.ts: a підготовчий курінь
 * has no number until it is given one in its settings, and a senior kurin goes by its гілки.
 */

export type KurinBranchName = 'UPYu' | 'USP' | 'UPS';

/** A гілка and the kurin's number there (web KurinNumbering). */
export interface KurinNumbering {
  branch: KurinBranchName;
  /** Null for a підготовчий курінь. */
  number: number | null;
}

const BRANCH_SHORT: Record<KurinBranchName, string> = { UPYu: 'УПЮ', USP: 'УСП', UPS: 'УПС' };

/** УСП чи УПС — старші гілки. */
export function isSeniorBranch(branch: KurinBranchName | null | undefined): boolean {
  return branch === 'USP' || branch === 'UPS';
}

/** «к. ч. 47», «підг. курінь»; a senior kurin by its main гілка, «5-й курінь УСП». */
export function kurinLabel(kurinNumber: number | null | undefined, branch?: KurinBranchName | null): string {
  if (branch && isSeniorBranch(branch)) return seniorLabel({ branch, number: kurinNumber ?? null });
  return kurinNumber === null || kurinNumber === undefined ? 'підг. курінь' : `к. ч. ${kurinNumber}`;
}

/** The same at the start of a sentence: «Курінь ч. 47», «Підготовчий курінь», «5-й курінь УСП». */
export function kurinName(kurinNumber: number | null | undefined, branch?: KurinBranchName | null): string {
  if (branch && isSeniorBranch(branch)) return seniorLabel({ branch, number: kurinNumber ?? null });
  return kurinNumber === null || kurinNumber === undefined ? 'Підготовчий курінь' : `Курінь ч. ${kurinNumber}`;
}

/** A heading: «47 курінь», «Підготовчий курінь». */
export function kurinTitle(kurinNumber: number | null | undefined): string {
  return kurinNumber === null || kurinNumber === undefined ? 'Підготовчий курінь' : `${kurinNumber} курінь`;
}

/** Every senior гілка with its number: «5-й курінь УСП · 20-й курінь УПС». */
export function kurinBranchesName(branches: readonly KurinNumbering[] | null | undefined): string {
  return (branches ?? []).filter((b) => isSeniorBranch(b.branch)).map(seniorLabel).join(' · ');
}

/** «УСП · УПС» — the гілки alone, for a tag. */
export function kurinBranchTags(branches: readonly KurinNumbering[] | null | undefined, fallback: KurinBranchName = 'UPYu'): string {
  const list = branches?.length ? branches.map((b) => b.branch) : [fallback];
  return list.map((branch) => BRANCH_SHORT[branch]).join(' · ');
}

function seniorLabel(numbering: KurinNumbering): string {
  const branch = BRANCH_SHORT[numbering.branch];
  return numbering.number === null ? `курінь ${branch}` : `${numbering.number}-й курінь ${branch}`;
}

/** «у курені ч. 47», «у підготовчому курені». */
export function inKurin(kurinNumber: number | null | undefined): string {
  return kurinNumber === null || kurinNumber === undefined ? 'підготовчому курені' : `курені ч. ${kurinNumber}`;
}

/** By number, підготовчі last. */
export function compareKurinNumbers(a: number | null | undefined, b: number | null | undefined): number {
  return (a ?? Number.MAX_SAFE_INTEGER) - (b ?? Number.MAX_SAFE_INTEGER);
}

/** A kurin in the middle of a sentence: «к. ч. 47», «підг. курінь», or a senior kurin by its гілки (web kurinInText). */
export function kurinInText(branches: readonly KurinNumbering[] | null | undefined, fallbackNumber?: number | null): string {
  return (branches ?? []).some((b) => isSeniorBranch(b.branch))
    ? kurinBranchesName(branches)
    : kurinLabel(branches?.[0]?.number ?? fallbackNumber);
}
