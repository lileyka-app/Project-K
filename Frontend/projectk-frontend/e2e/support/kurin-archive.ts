import { randomInt } from 'node:crypto';

import { APIRequestContext, expect, TestInfo } from '@playwright/test';

import { activateAccount, getLatestInvitationByEmail, loginViaApi } from './api-client';
import { e2eApiUrl } from './e2e-api';
import { createActivatedMemberAccount, scenarioEmail, scenarioSuffix } from './scenarios';
import { e2eUsers, E2eUser } from './test-users';

/**
 * A kurin of the test's own, opened by the admin with an invited Звʼязковий.
 *
 * The archive's danger zone is one per account and kurin and lives in the API's memory, and its
 * deletions are for good. Running both projects against the seeded kurin would let one end the
 * other's zone and delete what other specs read, so every test here brings its own kurin.
 */
export interface OwnKurin {
  kurinKey: string;
  suffix: string;
  founder: E2eUser;
  founderMemberKey: string;
  api: KurinArchiveApi;
}

export interface ArchiveMember {
  memberKey: string;
  name: string;
}

const founderPassword = 'Founder@12345';

export async function openOwnKurin(request: APIRequestContext, testInfo: TestInfo): Promise<OwnKurin> {
  const suffix = `${scenarioSuffix(testInfo)}${randomInt(1000)}`;
  const email = scenarioEmail(suffix, 'archive.founder');
  const admin = await loginViaApi(request, e2eUsers.admin);

  // No number: the onboarding specs draw theirs from 101–199 and have no retry, so a numbered kurin
  // here could take the one they picked.
  const response = await request.post(`${e2eApiUrl}/auth/register/kurin`, {
    headers: { Authorization: `Bearer ${admin.tokens!.accessToken}` },
    data: { email, firstName: `Founder${suffix}`, lastName: 'Архівна', kurinNumber: null }
  });
  expect(response.ok(), `Failed to open a kurin: ${response.status()} ${await response.text()}`).toBe(true);

  const invitation = await getLatestInvitationByEmail(request, email);
  await activateAccount(request, invitation.token, founderPassword);

  const founder: E2eUser = { role: 'manager', email, password: founderPassword };
  const login = await loginViaApi(request, founder);
  return {
    kurinKey: login.kurinKey!,
    suffix,
    founder,
    founderMemberKey: login.memberKey!,
    api: new KurinArchiveApi(request, founder, login.kurinKey!, login.tokens!.accessToken!)
  };
}

/** Builds an archive's history through the API, as the kurin's Звʼязковий. */
export class KurinArchiveApi {
  constructor(
    private readonly request: APIRequestContext,
    private readonly founder: E2eUser,
    readonly kurinKey: string,
    private readonly token: string
  ) {}

  private get headers(): Record<string, string> {
    return { Authorization: `Bearer ${this.token}` };
  }

  async group(name: string): Promise<string> {
    const response = await this.request.post(`${e2eApiUrl}/group`, {
      headers: this.headers,
      data: { groupKey: '00000000-0000-0000-0000-000000000000', kurinKey: this.kurinKey, name }
    });
    expect(response.ok(), `Failed to create group ${name}: ${response.status()} ${await response.text()}`).toBe(true);
    return (await response.json() as { groupKey: string }).groupKey;
  }

  async member(groupKey: string, firstName: string, lastName: string): Promise<ArchiveMember> {
    const multipart = new FormData();
    multipart.append('firstName', firstName);
    multipart.append('middleName', 'Тестович');
    multipart.append('lastName', lastName);
    multipart.append('email', `e2e.archive.${Date.now()}.${randomInt(1_000_000)}@example.com`);
    multipart.append('phoneNumber', '+380501234567');
    multipart.append('dateOfBirth', '2008-01-01');
    multipart.append('createUserAccount', 'false');
    multipart.append('groupKey', groupKey);

    const response = await this.request.post(`${e2eApiUrl}/member`, { headers: this.headers, multipart });
    expect(response.ok(), `Failed to create member ${lastName}: ${response.status()} ${await response.text()}`).toBe(true);
    return { memberKey: (await response.json() as { memberKey: string }).memberKey, name: `${firstName} ${lastName}` };
  }

  /** A member with an account of their own, who can sign in. */
  async memberWithAccount(groupKey: string, firstName: string, lastName: string, suffix: string): Promise<ArchiveMember & { user: E2eUser }> {
    const account = await createActivatedMemberAccount(
      this.request,
      this.founder,
      { groupKey },
      { firstName, middleName: 'Тестович', lastName, email: scenarioEmail(suffix, `archive.account.${randomInt(1_000_000)}`) },
      'Member@12345',
      'member'
    );
    return { memberKey: account.member.memberKey, name: `${firstName} ${lastName}`, user: account.user };
  }

  async membershipKey(memberKey: string): Promise<string> {
    const response = await this.request.get(`${e2eApiUrl}/member/${memberKey}/dossier/memberships`, { headers: this.headers });
    expect(response.ok(), `Failed to read memberships: ${response.status()} ${await response.text()}`).toBe(true);
    const memberships = await response.json() as { membershipKey: string; kurinKey: string }[];
    return memberships.find(membership => membership.kurinKey === this.kurinKey)!.membershipKey;
  }

  /** Releases the member: their membership closes today and goes to the archive. */
  async leave(memberKey: string): Promise<void> {
    const response = await this.request.delete(`${e2eApiUrl}/kurin/${this.kurinKey}/memberships/${memberKey}`, { headers: this.headers });
    expect(response.ok(), `Failed to release ${memberKey}: ${response.status()} ${await response.text()}`).toBe(true);
  }

  /**
   * Seats the гурток's провід. The API seats a holder from today and ends, as of today, whoever is
   * left out on the next call, so seating and then reseating without someone makes a former role.
   */
  async seatGroupLeaders(groupKey: string, holders: { member: ArchiveMember; role: string }[], leadershipKey?: string): Promise<string> {
    const data = {
      type: 'Group',
      entityKey: groupKey,
      startDate: new Date().toISOString().slice(0, 10),
      leadershipHistories: holders.map(({ member, role }) => {
        const [firstName, ...rest] = member.name.split(' ');
        return { role, member: { memberKey: member.memberKey, firstName, lastName: rest.join(' '), middleName: '' }, endDate: null };
      })
    };
    const response = leadershipKey
      ? await this.request.put(`${e2eApiUrl}/leadership/${leadershipKey}`, { headers: this.headers, data })
      : await this.request.post(`${e2eApiUrl}/leadership`, { headers: this.headers, data });
    expect(response.ok(), `Failed to seat the гурток's провід: ${response.status()} ${await response.text()}`).toBe(true);
    return leadershipKey ?? (await response.json() as { leadershipKey: string }).leadershipKey;
  }

  /** Puts money in a гурток's box: a member's внесок, or income that is nobody's. */
  async pay(groupKey: string, amount: number, membershipKey?: string): Promise<void> {
    const response = await this.request.post(`${e2eApiUrl}/group/${groupKey}/dues/entries`, {
      headers: this.headers,
      data: {
        kind: membershipKey ? 'Contribution' : 'OtherIncome',
        method: 'Cash',
        amount,
        occurredOn: new Date().toISOString().slice(0, 10),
        membershipKey: membershipKey ?? null
      }
    });
    expect(response.ok(), `Failed to record a payment: ${response.status()} ${await response.text()}`).toBe(true);
  }

  /**
   * Deletes a former membership the way the page does. The ticket belongs to the page's session, so
   * this takes the page's own access token, not the one this helper signed in with.
   */
  async deleteMembershipAsPage(membershipKey: string, ticket: string, pageToken: string): Promise<{ status: number; code: string | null }> {
    const response = await this.request.delete(`${e2eApiUrl}/kurin/${this.kurinKey}/archive/danger/memberships/${membershipKey}`, {
      headers: { Authorization: `Bearer ${pageToken}`, 'X-Archive-Danger': ticket }
    });
    const body = await response.json().catch(() => null) as { error?: string } | null;
    return { status: response.status(), code: body?.error ?? null };
  }
}
