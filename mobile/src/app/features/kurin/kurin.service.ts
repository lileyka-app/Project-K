import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { CountryCode } from 'libphonenumber-js';
import { firstValueFrom } from 'rxjs';
import { Api } from '../../core/api';
import { phoneCountryOfKrai } from '../../core/phone';
import { apiUrl } from '../../runtime-config';
import {
  BadgeCatalogItemDto,
  BadgeProgressDto,
  CardFields,
  GroupDto,
  GroupRequest,
  GroupedProbeDto,
  KurinDto,
  LeadershipDto,
  MemberAwardDto,
  MemberDto,
  MemberDuesDto,
  MemberLookupDto,
  MemberWarningDto,
  MembershipDto,
  MentorAssignmentDto,
  ProbeProgressDto,
  ProbeSummaryDto,
  UpsertAwardRequest,
  UpsertLeadershipRequest,
  WarningLevel,
} from './kurin.models';
import { KraiProfileCardFields, NewMemberPlacement, cardFieldsOf } from './member-edit';
import { browserTimeZone } from './kurin-profile';
import { MemberAbilities } from './abilities';

/**
 * The endpoints the web's kurinModule services call (kurin, group, member, leadership,
 * member-progress, catalogs, member-award) and the one dues read the card makes, as promises.
 */
@Injectable({ providedIn: 'root' })
export class KurinApi {
  private readonly api = inject(Api);

  /**
   * Bumped after a write to the гуртки or to someone's card, so the kurin tab and a гурток, kept
   * alive under the pages pushed over them, read their lists again.
   */
  readonly groupsChanged = signal(0);
  readonly membersChanged = signal(0);
  /** Bumped after a провід is saved: the kurin tab and the провід page above it read it again. */
  readonly leadershipChanged = signal(0);

  kurin(kurinKey: string): Promise<KurinDto> {
    return this.api.get(`kurin/${kurinKey}`);
  }

  /** The kurin's record (web kurin-panel «Редагування даних куреня»); the whole record goes back. */
  updateKurin(kurin: KurinDto): Promise<KurinDto> {
    return this.api.put(`kurin/${kurin.kurinKey}`, kurin);
  }

  /** What the kurin's край asks on a member's card (its profile's `cardFields`, or the default without one). */
  async cardFields(kurinKey: string): Promise<CardFields> {
    return cardFieldsOf(await this.api.get<KraiProfileCardFields>(`kurin/${kurinKey}/profile`));
  }

  /** The country a number typed without «+» is read in: the kurin's край's (CZ for cz). */
  async phoneCountry(kurinKey: string): Promise<CountryCode> {
    return phoneCountryOfKrai((await this.api.get<{ kraiCode?: string }>(`kurin/${kurinKey}/profile`)).kraiCode);
  }

  /** The kurin's report as a PDF (web «Експорт звіту куреня»), dated in the reader's zone. */
  reportPdf(kurinKey: string): Promise<Blob> {
    return this.api.blob(`kurin/${kurinKey}/report/pdf`, { timeZone: browserTimeZone() });
  }

  groups(kurinKey: string): Promise<GroupDto[]> {
    return this.api.get('group/groups', { kurinKey });
  }

  group(groupKey: string): Promise<GroupDto> {
    return this.api.get(`group/${groupKey}`);
  }

  kurinMembers(kurinKey: string): Promise<MemberLookupDto[]> {
    return this.api.get(`member/kurins/${kurinKey}/members`);
  }

  groupMembers(groupKey: string): Promise<MemberLookupDto[]> {
    return this.api.get(`member/groups/${groupKey}/members`);
  }

  kvMembers(kurinKey: string): Promise<MemberLookupDto[]> {
    return this.api.get(`member/members/kv/${kurinKey}`);
  }

  mentorAssignments(kurinKey: string): Promise<MentorAssignmentDto[]> {
    return this.api.get(`group/groups/${kurinKey}/mentor-assignments`);
  }

  leadership(type: 'kurin' | 'group' | 'kv', key: string): Promise<LeadershipDto> {
    return this.api.get(`leadership/type/${type}/${key}`);
  }

  /** The гуртки whose box this person keeps (web dues getReadableGroups). */
  duesGroups(kurinKey: string): Promise<{ groupKey: string; groupName: string }[]> {
    return this.api.get(`kurin/${kurinKey}/dues/groups`);
  }

  member(memberKey: string): Promise<MemberDto> {
    return this.api.get(`member/${memberKey}`);
  }

  memberships(memberKey: string): Promise<MembershipDto[]> {
    return this.api.get(`member/${memberKey}/dossier/memberships`);
  }

  /** A youth may read only their own; for anyone else the server refuses and the card shows none. */
  dues(memberKey: string): Promise<MemberDuesDto> {
    return this.api.get(`member/${memberKey}/dues`);
  }

  /** What the kurin's policy lets this person do to the member (web MemberService.getAbilities). */
  abilities(memberKey: string): Promise<MemberAbilities> {
    return this.api.get(`member/${memberKey}/abilities`);
  }

  /** The server's own answer whether this person may change the card (web EntityService). */
  async canUpdateMember(memberKey: string): Promise<boolean> {
    const answer = await this.api.post<unknown>('auth/check-access', { entityType: 'member', entityKey: memberKey, action: 'Update' });
    return answer === true;
  }

  badgeCatalog(): Promise<BadgeCatalogItemDto[]> {
    return this.api.get('catalog/badges', { take: 1000 });
  }

  /** Only the вмілості the kurin's край offers for a new start; what a member already has comes from badgeCatalog. */
  kurinBadgeCatalog(kurinKey: string): Promise<BadgeCatalogItemDto[]> {
    return this.api.get(`kurin/${kurinKey}/catalog/badges`);
  }

  badgeProgress(memberKey: string): Promise<BadgeProgressDto[]> {
    return this.api.get(`member/${memberKey}/badges/progress`);
  }

  submitBadge(memberKey: string, badgeId: string): Promise<BadgeProgressDto> {
    return this.api.post(`member/${memberKey}/badges/${badgeId}/submit`, { note: null });
  }

  reviewBadge(memberKey: string, badgeId: string, isApproved: boolean): Promise<BadgeProgressDto> {
    return this.api.post(`member/${memberKey}/badges/${badgeId}/review`, { isApproved, note: null });
  }

  probes(): Promise<ProbeSummaryDto[]> {
    return this.api.get('catalog/probes');
  }

  groupedProbe(probeId: string): Promise<GroupedProbeDto> {
    return this.api.get(`catalog/probes/${probeId}/grouped`);
  }

  probeProgress(memberKey: string, probeId: string): Promise<ProbeProgressDto> {
    return this.api.get(`member/${memberKey}/probes/${probeId}/progress`);
  }

  signPoint(memberKey: string, probeId: string, pointId: string, sign: boolean): Promise<ProbeProgressDto> {
    return this.api.put(`member/${memberKey}/probes/${probeId}/points/${pointId}/${sign ? 'sign' : 'unsign'}`, { note: null });
  }

  closeProbe(memberKey: string, probeId: string): Promise<ProbeProgressDto> {
    return this.api.put(`member/${memberKey}/probes/${probeId}/progress/status`, { status: 'Completed', note: null });
  }

  saveAward(memberKey: string, request: UpsertAwardRequest): Promise<MemberAwardDto> {
    return this.api.post(`member/${memberKey}/awards`, request);
  }

  deleteAward(memberKey: string, awardKey: string): Promise<void> {
    return this.api.delete(`member/${memberKey}/awards/${awardKey}`);
  }

  reviewAward(memberKey: string, awardKey: string, isApproved: boolean): Promise<MemberAwardDto> {
    return this.api.post(`member/${memberKey}/awards/${awardKey}/review`, { isApproved, note: null });
  }

  /** `PUT member/{key}`: the multipart body memberFormData builds (web MemberService.update). */
  async updateMember(memberKey: string, body: FormData): Promise<MemberDto> {
    const saved = await this.api.put<MemberDto>(`member/${memberKey}`, body);
    this.membersChanged.update((n) => n + 1);
    return saved;
  }

  /** A new member with an account and its invitation (web MemberService.create): into a гурток, or the kurin. */
  async createMember(placement: NewMemberPlacement, body: FormData): Promise<MemberDto> {
    const path = 'groupKey' in placement ? 'member' : `member/kurins/${placement.kurinKey}/members`;
    const created = await this.api.post<MemberDto>(path, body);
    this.membersChanged.update((n) => n + 1);
    return created;
  }

  async deleteMember(memberKey: string): Promise<void> {
    await this.api.delete(`member/${memberKey}`);
    this.membersChanged.update((n) => n + 1);
  }

  /** A fresh invitation for an account nobody has claimed yet; earlier links stop working. */
  resendInvitation(memberKey: string): Promise<void> {
    return this.api.post(`member/${memberKey}/invitation`, null);
  }

  verifyProfile(memberKey: string): Promise<MemberDto> {
    return this.api.put(`member/${memberKey}/profile-verification`, { note: null });
  }

  resetProfileVerification(memberKey: string): Promise<MemberDto> {
    return this.api.delete(`member/${memberKey}/profile-verification`);
  }

  assignWarning(memberKey: string, level: WarningLevel): Promise<MemberWarningDto> {
    return this.api.post(`member/${memberKey}/warnings`, { level });
  }

  cancelWarning(memberKey: string, warningKey: string): Promise<MemberWarningDto> {
    return this.api.delete(`member/${memberKey}/warnings/${warningKey}`);
  }

  // ── Гуртки (web GroupService) ──────────────────────────────────────────────────────────────────

  /** The server's own answer whether this person may change the гурток's profile and silhouette (web group-panel). */
  async canUpdateGroup(groupKey: string): Promise<boolean> {
    const answer = await this.api.post<unknown>('auth/check-access', { entityType: 'group', entityKey: groupKey, action: 'Update' });
    return answer === true;
  }

  createGroup(kurinKey: string, request: GroupRequest): Promise<GroupDto> {
    return this.changingGroups(this.api.post<GroupDto>('group', { ...request, kurinKey }));
  }

  updateGroup(groupKey: string, request: GroupRequest): Promise<GroupDto> {
    return this.changingGroups(this.api.put<GroupDto>(`group/${groupKey}`, request));
  }

  deleteGroup(groupKey: string): Promise<void> {
    return this.changingGroups(this.api.delete<void>(`group/${groupKey}`));
  }

  uploadSilhouette(groupKey: string, image: Blob, name: string): Promise<GroupDto> {
    const body = new FormData();
    body.append('file', image, name);
    return this.changingGroups(this.api.post<GroupDto>(`group/${groupKey}/silhouette`, body));
  }

  deleteSilhouette(groupKey: string): Promise<GroupDto> {
    return this.changingGroups(this.api.delete<GroupDto>(`group/${groupKey}/silhouette`));
  }

  /** The server's check-access answer for adding a new member to this гурток (web canCreateMembers). */
  async canCreateInGroup(groupKey: string): Promise<boolean> {
    const answer = await this.api.post<unknown>('auth/check-access', { entityType: 'group', entityKey: groupKey, action: 'Create' });
    return answer === true;
  }

  // ── Склад гуртка: membership placement, виховники, провід (web MembershipService, GroupService, LeadershipService) ──

  /** Into another гурток of the kurin, or out of any with null; the person stays in the kurin. */
  async moveToGroup(kurinKey: string, memberKey: string, groupKey: string | null): Promise<void> {
    await this.api.put(`kurin/${kurinKey}/memberships/${memberKey}/group`, { groupKey });
    this.membersChanged.update((n) => n + 1);
  }

  /** Closes the membership in this kurin (web «Вивести»); the person and their history stay. */
  async leaveKurin(kurinKey: string, memberKey: string): Promise<void> {
    await this.api.delete(`kurin/${kurinKey}/memberships/${memberKey}`);
    this.membersChanged.update((n) => n + 1);
  }

  /** The КВ people who may be a гурток's виховник (the Звʼязковий only). */
  mentorCandidates(kurinKey: string): Promise<MemberLookupDto[]> {
    return this.api.get(`member/members/mentor-candidates/${kurinKey}`);
  }

  groupMentors(groupKey: string): Promise<MemberLookupDto[]> {
    return this.api.get(`group/${groupKey}/mentors`);
  }

  /**
   * Assigns and revokes in one go, as the web's dialog does; the КВ list on the kurin tab follows.
   * Rejects with the first failure once every call has been made.
   */
  async saveMentors(groupKey: string, changes: { assign: string[]; revoke: string[] }): Promise<void> {
    const results = await Promise.allSettled([
      ...changes.assign.map((userKey) => this.api.post(`group/${groupKey}/mentors/${userKey}`, null)),
      ...changes.revoke.map((userKey) => this.api.delete(`group/${groupKey}/mentors/${userKey}`)),
    ]);
    this.groupsChanged.update((n) => n + 1);
    const failed = results.find((r): r is PromiseRejectedResult => r.status === 'rejected');
    if (failed) throw failed.reason;
  }

  /**
   * One впорядник's гуртки (web kv-panel «Налаштування впорядника»): assigns and revokes in one go;
   * rejects with the first failure once every call has been made.
   */
  async saveMentorGroups(userKey: string, changes: { assign: string[]; revoke: string[] }): Promise<void> {
    const results = await Promise.allSettled([
      ...changes.assign.map((groupKey) => this.api.post(`group/${groupKey}/mentors/${userKey}`, null)),
      ...changes.revoke.map((groupKey) => this.api.delete(`group/${groupKey}/mentors/${userKey}`)),
    ]);
    this.groupsChanged.update((n) => n + 1);
    const failed = results.find((r): r is PromiseRejectedResult => r.status === 'rejected');
    if (failed) throw failed.reason;
  }

  /** A first провід is created; one already there is updated by its key. */
  async saveLeadership(leadershipKey: string | null, request: UpsertLeadershipRequest): Promise<LeadershipDto> {
    const saved = leadershipKey
      ? await this.api.put<LeadershipDto>(`leadership/${leadershipKey}`, { ...request, leadershipKey })
      : await this.api.post<LeadershipDto>('leadership', request);
    this.membersChanged.update((n) => n + 1);
    this.leadershipChanged.update((n) => n + 1);
    // The КВ list reads offices too.
    if (request.type === 'KV') this.groupsChanged.update((n) => n + 1);
    return saved;
  }

  private async changingGroups<T>(request: Promise<T>): Promise<T> {
    const result = await request;
    this.groupsChanged.update((n) => n + 1);
    return result;
  }
}

/**
 * Badge and award pictures sit behind the API's auth, so they come as blobs with the token and are
 * shown from object URLs (web BadgeImageBlobService). A picture that fails stays null: the row
 * shows its icon instead.
 */
@Injectable({ providedIn: 'root' })
export class ProtectedImages {
  private readonly http = inject(HttpClient);
  private readonly base = apiUrl();
  private readonly origin = originOf(this.base);
  private readonly urls = signal<Record<string, string | null>>({});
  private readonly pending = new Set<string>();

  /**
   * A catalogue badge's picture. The catalogue serves it publicly, so it loads as is: fetching it here
   * would send the access token to another host.
   */
  badge(imagePath: string | null | undefined): string | null {
    if (!imagePath) return null;
    if (/^(https?:|data:)/.test(imagePath)) {
      return imagePath.startsWith(`${this.origin}/`) ? this.resolve(imagePath) : imagePath;
    }
    const path = imagePath.replace(/^\//, '');
    const relative = path.startsWith('badges_images/') ? path : `badges_images/${path}`;
    return this.resolve(`${this.origin}/${relative}`);
  }

  /** An award's medal, coloured once confirmed. */
  award(award: MemberAwardDto, level: number, confirmed: boolean): string | null {
    return this.resolve(award.imageUrl || `${this.base}/awards/images/${level}?colored=${confirmed}`);
  }

  private resolve(url: string): string | null {
    const known = this.urls()[url];
    if (known !== undefined) return known;
    if (!this.pending.has(url)) {
      this.pending.add(url);
      firstValueFrom(this.http.get(url, { responseType: 'blob' })).then(
        (blob) => this.urls.update((all) => ({ ...all, [url]: URL.createObjectURL(blob) })),
        () => this.urls.update((all) => ({ ...all, [url]: null })),
      );
    }
    return null;
  }
}

function originOf(url: string): string {
  try {
    return new URL(url).origin;
  } catch {
    return '';
  }
}
