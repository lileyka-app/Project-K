import { Injectable, inject } from '@angular/core';
import { Api } from '../../core/api';
import { periodApiQuery } from './score.format';
import {
  AgendaItemStartDto,
  AttendanceSheetDto,
  GroupScoreDto,
  KurinScoreDto,
  MarkAttendanceResultDto,
  ScorePeriodQuery,
  SetScoreAttendanceRateRequest,
  UpsertScoreEntryRequest,
} from './score.models';

/**
 * The web's ScoreService (scoreModule/services/score-service) for the screens the phone has:
 * the table, a гурток's page, the sheet of one event and points by hand. Nothing is cached:
 * the sheet is edited on the spot, and the other two are a pull away from fresh.
 */
@Injectable({ providedIn: 'root' })
export class ScoreService {
  private readonly api = inject(Api);

  kurin(kurinKey: string, period: ScorePeriodQuery = {}): Promise<KurinScoreDto> {
    return this.api.get(`kurin/${kurinKey}/score`, periodApiQuery(period));
  }

  group(kurinKey: string, groupKey: string, period: ScorePeriodQuery = {}): Promise<GroupScoreDto> {
    return this.api.get(`kurin/${kurinKey}/score/groups/${groupKey}`, periodApiQuery(period));
  }

  sheet(kurinKey: string, agendaItemKey: string, occurrence: string): Promise<AttendanceSheetDto> {
    return this.api.get(this.event(kurinKey, agendaItemKey, occurrence));
  }

  mark(kurinKey: string, agendaItemKey: string, occurrence: string, membershipKeys: string[]): Promise<MarkAttendanceResultDto[]> {
    return this.api.post(`${this.event(kurinKey, agendaItemKey, occurrence)}/attendance`, { membershipKeys });
  }

  unmark(kurinKey: string, agendaItemKey: string, occurrence: string, membershipKey: string): Promise<unknown> {
    return this.api.delete(`${this.event(kurinKey, agendaItemKey, occurrence)}/attendance/${membershipKey}`);
  }

  /** «Сходин не було», or taking it back; the server refuses the first while anyone is marked. */
  setNotHeld(kurinKey: string, agendaItemKey: string, occurrence: string, notHeld: boolean): Promise<unknown> {
    const path = `${this.event(kurinKey, agendaItemKey, occurrence)}/not-held`;
    return notHeld ? this.api.put(path, {}) : this.api.delete(path);
  }

  createEntry(kurinKey: string, request: UpsertScoreEntryRequest): Promise<unknown> {
    return this.api.post(`kurin/${kurinKey}/score/entries`, request);
  }

  updateEntry(kurinKey: string, entryKey: string, request: UpsertScoreEntryRequest): Promise<unknown> {
    return this.api.put(`kurin/${kurinKey}/score/entries/${entryKey}`, request);
  }

  deleteEntry(kurinKey: string, entryKey: string): Promise<unknown> {
    return this.api.delete(`kurin/${kurinKey}/score/entries/${entryKey}`);
  }

  /** An event's own rate; `points: null` gives it back to its group of events. */
  setRate(kurinKey: string, request: SetScoreAttendanceRateRequest): Promise<unknown> {
    return this.api.put(`kurin/${kurinKey}/score/settings/attendance-rates`, request);
  }

  /** The item itself, for its start when a link brought no occurrence (a one-off event). */
  agendaItem(agendaItemKey: string): Promise<AgendaItemStartDto> {
    return this.api.get(`agenda/item/${agendaItemKey}`);
  }

  /** The occurrence is its start as an ISO instant; it has colons, so it goes encoded. */
  private event(kurinKey: string, agendaItemKey: string, occurrence: string): string {
    return `kurin/${kurinKey}/score/events/${agendaItemKey}/${encodeURIComponent(occurrence)}`;
  }
}
