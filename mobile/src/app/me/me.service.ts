import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { apiUrl } from '../runtime-config';
import { TileLayoutDto } from './home-layout';
import {
  AgendaItemStatus,
  AgendaRsvpStatus,
  MemberDto,
  MyDuesDto,
  MyDutiesDto,
  MyDutyDto,
  MyEventDto,
  MyGrowthDto,
  MyScoreDto,
  MyTaskDto,
} from './me.models';

/** What the phone reads about the signed-in person: the same endpoints as the web dashboard. */
@Injectable({ providedIn: 'root' })
export class MeService {
  private readonly http = inject(HttpClient);
  private readonly api = apiUrl();

  events(days = 14): Promise<MyEventDto[]> {
    return this.get(`me/events?days=${days}`);
  }

  tasks(): Promise<MyTaskDto[]> {
    return this.get('me/tasks');
  }

  growth(): Promise<MyGrowthDto> {
    return this.get('me/growth');
  }

  dues(): Promise<MyDuesDto[]> {
    return this.get('me/dues');
  }

  /** What waits on the person as провід, less what they hid; empty for a youth. */
  async duties(): Promise<MyDutiesDto> {
    return dutiesOf(await this.get<MyDutiesDto | LegacyDuty[]>('me/duties/board'));
  }

  /** Hides one duty from the person's own «Справи» until it grows. */
  dismissDuty(duty: MyDutyDto): Promise<unknown> {
    return firstValueFrom(this.http.post(`${this.api}/me/duties/dismissed`, { key: duty.key, fingerprint: duty.fingerprint }));
  }

  /** Brings every hidden duty back. */
  restoreDuties(): Promise<unknown> {
    return firstValueFrom(this.http.delete(`${this.api}/me/duties/dismissed`));
  }

  score(): Promise<MyScoreDto[]> {
    return this.get('me/score');
  }

  /** Every board's saved layout (web TileLayoutService); the API has no read of a single board. */
  layouts(): Promise<TileLayoutDto[]> {
    return this.get('user/me/layouts');
  }

  member(memberKey: string): Promise<MemberDto> {
    return this.get(`member/${memberKey}`);
  }

  /** The answer to one occurrence; a series is answered per occurrence, by its start. */
  respond(event: MyEventDto, status: AgendaRsvpStatus): Promise<unknown> {
    const occurrenceStartUtc = event.isRecurring ? event.startUtc : null;
    return firstValueFrom(
      this.http.put(`${this.api}/me/events/${event.agendaItemKey}/response`, { status, occurrenceStartUtc }),
    );
  }

  /** Through the board's own endpoint, as the web dashboard does. */
  moveTask(task: MyTaskDto, status: AgendaItemStatus): Promise<unknown> {
    return firstValueFrom(this.http.put(`${this.api}/agenda/${task.agendaItemKey}/status`, { status }));
  }

  private get<T>(path: string): Promise<T> {
    return firstValueFrom(this.http.get<T>(`${this.api}/${path}`));
  }
}

/** A duty as a server that cannot hide one sends it. */
type LegacyDuty = Omit<MyDutyDto, 'key' | 'fingerprint'> & Partial<Pick<MyDutyDto, 'key' | 'fingerprint'>>;

/** A server a deploy has not reached yet answers with the bare list: nothing hidden, nothing to hide. */
export function dutiesOf(reply: MyDutiesDto | LegacyDuty[]): MyDutiesDto {
  if (Array.isArray(reply)) {
    return { items: reply.map((duty) => ({ ...duty, key: duty.key ?? '', fingerprint: duty.fingerprint ?? '' })), hiddenCount: 0 };
  }
  return reply;
}
