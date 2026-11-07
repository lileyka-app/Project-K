import { KurinNumbering } from './enums/kurin-branch.enum';

/** Запит старшого куреня змінити гілки й числа. Вирішує адміністратор. */
export interface KurinBranchRequestDto {
  kurinBranchRequestKey: string;
  kurinKey: string;
  kraiCode: string;
  kurinNamedAfter: string | null;
  /** Що курінь має зараз, головна гілка першою. */
  currentBranches: KurinNumbering[];
  /** Що просить натомість. */
  wantedBranches: KurinNumbering[];
  note: string | null;
  status: 'Pending' | 'Approved' | 'Rejected';
  requestedByName: string | null;
  requestedAtUtc: string;
}

/** Порожнє число — курінь виходить із цієї гілки. */
export interface SubmitKurinBranchRequest {
  uspNumber: number | null;
  upsNumber: number | null;
  note: string | null;
}
