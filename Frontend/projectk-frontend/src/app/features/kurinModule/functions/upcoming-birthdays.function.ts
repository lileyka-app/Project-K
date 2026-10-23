import { MemberLookupDto } from '../models/requests/member/member-lookup.dto';
import { parseDateOnlyString } from './to-date-only-string.function';

export interface UpcomingBirthdayItem {
  member: MemberLookupDto;
  nextBirthdayDate: Date;
  daysUntilBirthday: number;
}

function toStartOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

/** Місяць (0–11) і день народження: з `birthday` («MM-DD»), а без нього — з повної дати. */
function resolveMonthAndDay(member: MemberLookupDto): { month: number; day: number } | null {
  const birthday = /^(\d{2})-(\d{2})$/.exec(member.birthday ?? '');
  if (birthday) {
    return { month: Number(birthday[1]) - 1, day: Number(birthday[2]) };
  }

  const parsedDate = parseDateOnlyString(member.dateOfBirth);
  if (!parsedDate || Number.isNaN(parsedDate.getTime())) {
    return null;
  }

  return { month: parsedDate.getMonth(), day: parsedDate.getDate() };
}

function resolveNextBirthdayDate(member: MemberLookupDto, today: Date): Date | null {
  const born = resolveMonthAndDay(member);
  if (!born) {
    return null;
  }

  let nextBirthdayDate = new Date(today.getFullYear(), born.month, born.day);
  if (nextBirthdayDate < today) {
    nextBirthdayDate = new Date(today.getFullYear() + 1, born.month, born.day);
  }

  return nextBirthdayDate;
}

function calculateDaysBetween(startDate: Date, endDate: Date): number {
  const millisecondsInDay = 1000 * 60 * 60 * 24;
  return Math.floor((endDate.getTime() - startDate.getTime()) / millisecondsInDay);
}

export function buildUpcomingBirthdays(
  members: MemberLookupDto[],
  daysAhead: number,
  referenceDate: Date = new Date()
): UpcomingBirthdayItem[] {
  const today = toStartOfDay(referenceDate);

  return members
    .map(member => {
      const nextBirthdayDate = resolveNextBirthdayDate(member, today);
      if (!nextBirthdayDate) {
        return null;
      }

      const daysUntilBirthday = calculateDaysBetween(today, nextBirthdayDate);
      return {
        member,
        nextBirthdayDate,
        daysUntilBirthday
      } satisfies UpcomingBirthdayItem;
    })
    .filter((item): item is UpcomingBirthdayItem => item !== null)
    .filter(item => item.daysUntilBirthday >= 0 && item.daysUntilBirthday <= daysAhead)
    .sort((a, b) => {
      if (a.daysUntilBirthday !== b.daysUntilBirthday) {
        return a.daysUntilBirthday - b.daysUntilBirthday;
      }

      return `${a.member.lastName} ${a.member.firstName}`.localeCompare(`${b.member.lastName} ${b.member.firstName}`);
    });
}
