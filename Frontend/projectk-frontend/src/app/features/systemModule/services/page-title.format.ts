import { KurinBranch, isSeniorBranch } from '../../kurinModule/models/enums/kurin-branch.enum';
import { kurinLabel } from '../../../shared/functions/kurin-label.function';

export const TITLE_SEPARATOR = ' · ';

export type TitleContextType = 'kurin' | 'group' | 'member';

/** «к. ч. 12»; a senior kurin by its own name, else by its main гілка, «5-й курінь УСП». */
export function formatKurinTitle(kurinNumber: number | null | undefined, branch?: KurinBranch | null, name?: string | null): string | null {
  if (name && isSeniorBranch(branch)) return name;
  return typeof kurinNumber === 'number' && Number.isFinite(kurinNumber)
    ? kurinLabel(kurinNumber, branch)
    : null;
}

export function formatGroupTitle(name: string | null | undefined): string | null {
  const trimmed = name?.trim();
  return trimmed ? `г. ${trimmed}` : null;
}

export function formatMemberTitle(
  lastName: string | null | undefined,
  firstName: string | null | undefined
): string | null {
  const parts = [lastName?.trim(), firstName?.trim()].filter(
    (part): part is string => !!part
  );

  return parts.length > 0 ? parts.join(' ') : null;
}

export function composePageTitle(context: string | null, appName: string): string {
  const trimmedContext = context?.trim();
  return trimmedContext ? `${trimmedContext}${TITLE_SEPARATOR}${appName}` : appName;
}
