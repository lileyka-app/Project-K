import { KurinBranch, KurinNumbering } from './enums/kurin-branch.enum';
import { MembershipKind } from './enums/membership-kind.enum';

/** Курінь, у якому цей акаунт може діяти зараз. Приходить із `auth/kurin-scope/options`. */
export interface KurinScopeOption {
  kurinKey: string;
  /** Null for a підготовчий курінь. */
  kurinNumber: number | null;
  branch: KurinBranch;
  /** Every гілка with the kurin's number there, main one first. */
  branches?: KurinNumbering[];
  namedAfter?: string | null;
  kind: MembershipKind;
}
