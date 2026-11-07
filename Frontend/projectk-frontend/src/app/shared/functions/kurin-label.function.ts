import { KURIN_BRANCH_LABELS, KurinBranch, KurinNumbering, isSeniorBranch } from '../../features/kurinModule/models/enums/kurin-branch.enum';

/**
 * How a kurin is named in a line of text: «к. ч. 47», or «підг. курінь» for a підготовчий курінь,
 * which has no number until it is given one in its settings. A senior kurin goes by its гілка:
 * «5-й курінь УСП».
 */
export function kurinLabel(kurinNumber: number | null | undefined, branch?: KurinBranch | null): string {
  if (branch && isSeniorBranch(branch)) {
    return seniorKurinLabel({ branch, number: kurinNumber ?? null });
  }
  return kurinNumber === null || kurinNumber === undefined ? 'підг. курінь' : `к. ч. ${kurinNumber}`;
}

/** The same at the start of a sentence: «Курінь ч. 47», «Підготовчий курінь», «5-й курінь УСП». */
export function kurinName(kurinNumber: number | null | undefined, branch?: KurinBranch | null): string {
  if (branch && isSeniorBranch(branch)) {
    return seniorKurinLabel({ branch, number: kurinNumber ?? null });
  }
  return kurinNumber === null || kurinNumber === undefined ? 'Підготовчий курінь' : `Курінь ч. ${kurinNumber}`;
}

/**
 * Every гілка of a kurin with its number there: «5-й курінь УСП · 20-й курінь УПС». A УПЮ kurin
 * reads as before, «Курінь ч. 47». The server's KurinBranchNames says the same in letters.
 */
export function kurinBranchesName(branches: readonly KurinNumbering[] | null | undefined, fallbackNumber?: number | null): string {
  const senior = (branches ?? []).filter(b => isSeniorBranch(b.branch));
  if (senior.length === 0) {
    return kurinName(branches?.[0]?.number ?? fallbackNumber);
  }
  return senior.map(seniorKurinLabel).join(' · ');
}

/** «УСП · УПС» — the гілки alone, for a tag. */
export function kurinBranchTags(branches: readonly KurinNumbering[] | null | undefined, fallback: KurinBranch = KurinBranch.UPYu): string {
  const list = branches?.length ? branches.map(b => b.branch) : [fallback];
  return list.map(branch => KURIN_BRANCH_LABELS[branch]).join(' · ');
}

function seniorKurinLabel(numbering: KurinNumbering): string {
  const branch = KURIN_BRANCH_LABELS[numbering.branch];
  return numbering.number === null ? `курінь ${branch}` : `${numbering.number}-й курінь ${branch}`;
}

/** Sorts kurins by number, підготовчі last. */
export function compareKurinNumbers(a: number | null | undefined, b: number | null | undefined): number {
  return (a ?? Number.MAX_SAFE_INTEGER) - (b ?? Number.MAX_SAFE_INTEGER);
}

/** What a kurin's record carries for its name, from any of the DTOs that describe one. */
export interface KurinNaming {
  number: number | null | undefined;
  branch?: KurinBranch | null;
  branches?: readonly KurinNumbering[] | null;
  namedAfter?: string | null;
}

/**
 * A kurin with its name on one line: "к. ч. 47 ім. Івана Богуна" for УПЮ. A senior kurin goes by its
 * own name first and its гілки after: "Орден Хрестоносців · 5-й курінь УСП · 20-й курінь УПС".
 */
export function kurinWithName(kurin: KurinNaming): string {
  if (kurin.branch && isSeniorBranch(kurin.branch)) {
    const numbers = seniorNumbers(kurin);
    return kurin.namedAfter ? `${kurin.namedAfter} · ${numbers}` : numbers;
  }
  const label = kurinLabel(kurin.number);
  return kurin.namedAfter ? `${label} ім. ${kurin.namedAfter}` : label;
}

/**
 * A kurin heading a group in a list, where УПЮ and senior kurins sit side by side: «Курінь ч. 5 УПЮ
 * ім. Івана Богуна», and a senior one by its name and гілки, «Орден Хрестоносців · 5-й курінь УСП».
 */
export function kurinGroupTitle(kurin: KurinNaming): string {
  if (kurin.branch && isSeniorBranch(kurin.branch)) {
    return kurinWithName(kurin);
  }
  const name = `${kurinName(kurin.number)} ${KURIN_BRANCH_LABELS[KurinBranch.UPYu]}`;
  return kurin.namedAfter ? `${name} ім. ${kurin.namedAfter}` : name;
}

/**
 * УПЮ kurins first, then senior ones, by number inside each and підготовчі last. A number is the
 * kurin's own in its улад, so 5-й УПЮ and 5-й УСП stay two kurins.
 */
export function compareKurinsByBranch(a: KurinNaming, b: KurinNaming): number {
  return Number(isSeniorBranch(a.branch)) - Number(isSeniorBranch(b.branch))
    || compareKurinNumbers(a.number, b.number);
}

/**
 * The first of two lines naming a kurin: a senior kurin's own name ("Орден Хрестоносців"), or its
 * гілки when it has none; a УПЮ kurin as before, "к. ч. 47 ім. Івана Богуна".
 */
export function kurinHeadline(kurin: KurinNaming): string {
  if (kurin.branch && isSeniorBranch(kurin.branch)) {
    return kurin.namedAfter || seniorNumbers(kurin);
  }
  return kurinWithName(kurin);
}

/** The second line under it: a senior kurin's гілки in full, "5-й курінь УСП · 20-й курінь УПС"; "УПЮ" otherwise. */
export function kurinSubline(kurin: KurinNaming): string {
  if (kurin.branch && isSeniorBranch(kurin.branch)) {
    return seniorNumbers(kurin);
  }
  return kurinBranchTags(kurin.branches, kurin.branch ?? KurinBranch.UPYu);
}

function seniorNumbers(kurin: KurinNaming): string {
  return kurinBranchesName(kurin.branches?.length ? kurin.branches : [{ branch: kurin.branch!, number: kurin.number ?? null }]);
}

/**
 * A kurin in the middle of a sentence: «к. ч. 47», «підг. курінь», or a senior kurin by its гілки,
 * «5-й курінь УСП» — never by the УПЮ number it happens to share. The server's KurinBranchNames.InText.
 */
export function kurinInText(branches: readonly KurinNumbering[] | null | undefined, fallbackNumber?: number | null): string {
  return (branches ?? []).some(b => isSeniorBranch(b.branch))
    ? kurinBranchesName(branches)
    : kurinLabel(branches?.[0]?.number ?? fallbackNumber);
}
