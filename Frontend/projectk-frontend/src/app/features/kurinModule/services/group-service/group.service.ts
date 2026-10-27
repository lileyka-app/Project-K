import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../../../../environments/environment';
import { Observable, tap } from 'rxjs';
import { GroupDto } from '../../models/group.dto';
import { CreateGroupDto } from '../../models/requests/create-group.dto';
import { UpdateGroupDto } from '../../models/requests/update-group.dto';
import { MemberLookupDto } from '../../models/requests/member/member-lookup.dto';
import { ClientCacheService } from '../client-cache/client-cache.service';
import { KurinProbeEditionsDto } from '../../models/group-probe-edition.dto';
import { ENTITY_CACHE_TTL_MS, GROUP_CACHE_PREFIX, MEMBER_CACHE_PREFIX } from '../client-cache/cache-policy';

export interface MentorAssignmentDto {
  mentorAssignmentKey: string;
  mentorUserKey: string;
  groupKey: string;
  groupName: string;
  assignedAtUtc: string;
  revokedAtUtc: string | null;
  member: MemberLookupDto | null;
}

@Injectable({
  providedIn: 'root'
})
export class GroupService {
  private readonly http = inject(HttpClient);
  private readonly cache = inject(ClientCacheService);
  private readonly apiUrl = `${environment.apiUrl}/group`;

  getByKey(groupKey: string): Observable<GroupDto> {
    return this.cache.get(
      `${GROUP_CACHE_PREFIX}by-key:${groupKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<GroupDto>(`${this.apiUrl}/${groupKey}`)
    );
  }

  getAllByKurinKey(kurinKey: string): Observable<GroupDto[]> {
    return this.cache.get(
      `${GROUP_CACHE_PREFIX}list:kurin:${kurinKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<GroupDto[]>(`${this.apiUrl}/groups`, { params: { kurinKey } })
    );
  }

  create(request: CreateGroupDto): Observable<GroupDto> {
    return this.http.post<GroupDto>(`${this.apiUrl}`, request).pipe(
      this.invalidateAfterGroupMutation()
    );
  }

  update(groupKey: string, request: UpdateGroupDto): Observable<GroupDto> {
    return this.http.put<GroupDto>(`${this.apiUrl}/${groupKey}`, request).pipe(
      this.invalidateAfterGroupMutation()
    );
  }

  uploadSilhouette(groupKey: string, file: File | Blob): Observable<GroupDto> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<GroupDto>(`${this.apiUrl}/${groupKey}/silhouette`, formData).pipe(
      this.invalidateAfterGroupMutation()
    );
  }

  deleteSilhouette(groupKey: string): Observable<GroupDto> {
    return this.http.delete<GroupDto>(`${this.apiUrl}/${groupKey}/silhouette`).pipe(
      this.invalidateAfterGroupMutation()
    );
  }

  delete(groupKey: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${groupKey}`).pipe(
      this.invalidateAfterGroupMutation()
    );
  }
    
  exists(groupKey: string): Observable<boolean> {
    return this.cache.get(
      `${GROUP_CACHE_PREFIX}exists:${groupKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<boolean>(`${this.apiUrl}/exists/${groupKey}`)
    );
  }

  /** Проба кожного гуртка куреня, для налаштувань куреня. */
  getKurinProbeEditions(kurinKey: string): Observable<KurinProbeEditionsDto> {
    return this.http.get<KurinProbeEditionsDto>(`${environment.apiUrl}/kurin/${kurinKey}/probe-editions`);
  }

  setProbeEdition(groupKey: string, edition: string | null): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${groupKey}/probe-edition`, { edition });
  }

  getMentors(groupKey: string): Observable<MemberLookupDto[]> {
    return this.cache.get(
      `${GROUP_CACHE_PREFIX}mentors:${groupKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<MemberLookupDto[]>(`${this.apiUrl}/${groupKey}/mentors`)
    );
  }

  getMentorAssignments(kurinKey: string): Observable<MentorAssignmentDto[]> {
    return this.cache.get(
      `${GROUP_CACHE_PREFIX}mentor-assignments:${kurinKey}`,
      ENTITY_CACHE_TTL_MS,
      () => this.http.get<MentorAssignmentDto[]>(`${this.apiUrl}/groups/${kurinKey}/mentor-assignments`)
    );
  }

  assignMentor(groupKey: string, mentorUserKey: string): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/${groupKey}/mentors/${mentorUserKey}`, null).pipe(
      this.invalidateAfterMentorAssignmentMutation()
    );
  }

  revokeMentor(groupKey: string, mentorUserKey: string): Observable<unknown> {
    return this.http.delete(`${this.apiUrl}/${groupKey}/mentors/${mentorUserKey}`).pipe(
      this.invalidateAfterMentorAssignmentMutation()
    );
  }

  private invalidateAfterGroupMutation<T>() {
    return (source: Observable<T>) => source.pipe(
      tap(() => {
        this.cache.invalidateByPrefix(GROUP_CACHE_PREFIX);
        this.cache.invalidateByPrefix(MEMBER_CACHE_PREFIX);
      })
    );
  }

  private invalidateAfterMentorAssignmentMutation<T>() {
    return (source: Observable<T>) => source.pipe(
      tap(() => {
        this.cache.invalidateByPrefix(`${GROUP_CACHE_PREFIX}mentors:`);
        this.cache.invalidateByPrefix(`${GROUP_CACHE_PREFIX}mentor-assignments:`);
        this.cache.invalidateByPrefix(`${MEMBER_CACHE_PREFIX}kv:`);
        this.cache.invalidateByPrefix(`${MEMBER_CACHE_PREFIX}mentor-candidates:`);
      })
    );
  }
}
