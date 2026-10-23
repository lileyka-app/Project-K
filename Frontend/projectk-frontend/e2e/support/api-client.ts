import { APIRequestContext, expect, Page } from '@playwright/test';
import { e2eApiUrl, e2eResetToken } from './e2e-api';
import { E2eUser } from './test-users';

interface LoginResponse {
  tokens?: {
    accessToken?: string;
  };
  kurinKey?: string;
  memberKey?: string;
  permissions?: string[];
  roles?: string[];
  userKey?: string;
}

export interface KurinScopeOption {
  kurinKey: string;
  kurinNumber: number;
  branch: string;
  namedAfter?: string | null;
  kind: string;
}

export interface MembershipRecord {
  membershipKey: string;
  kurinKey: string;
  kurinNumber: number;
  branch: string;
  isCurrent: boolean;
}

interface GroupResponse {
  groupKey: string;
  name: string;
}

interface MemberResponse {
  memberKey: string;
  userKey?: string | null;
  userRole?: string | null;
  firstName?: string;
  middleName?: string;
  lastName?: string;
  email?: string;
}

interface PlanningSessionResponse {
  planningSessionKey: string;
  name: string;
}

interface InvitationResponse {
  invitationKey: string;
  token: string;
  waitlistEntryKey: string;
  targetUserKey?: string | null;
  email: string;
}

interface WaitlistEntryResponse {
  waitlistEntryKey: string;
  email: string;
}

interface KurinResponse {
  kurinKey: string;
  number: number;
}

export interface CreatedMember {
  memberKey: string;
  userKey?: string | null;
  email: string;
  firstName: string;
  middleName: string;
  lastName: string;
}

export interface CreatedGroup {
  groupKey: string;
  name: string;
}

export async function loginViaApi(request: APIRequestContext, user: E2eUser): Promise<LoginResponse> {
  const response = await request.post(`${e2eApiUrl}/auth/login`, {
    data: {
      email: user.email,
      password: user.password
    }
  });

  expect(response.ok(), `API login failed for ${user.email}: ${response.status()} ${await response.text()}`).toBe(true);

  return await response.json() as LoginResponse;
}

export async function interceptAuthRefresh(page: Page, request: APIRequestContext, user: E2eUser): Promise<void> {
  const login = await loginViaApi(request, user);
  await page.route('**/api/auth/refresh', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ accessToken: login.tokens?.accessToken })
    });
  });
}

export async function getLatestInvitationByEmail(request: APIRequestContext, email: string): Promise<InvitationResponse> {
  const response = await request.get(`${e2eApiUrl}/test/e2e/invitations/by-email`, {
    headers: {
      'X-E2E-Reset-Token': e2eResetToken
    },
    params: {
      email
    }
  });

  expect(response.ok(), `Failed to load E2E invitation for ${email}: ${response.status()} ${await response.text()}`).toBe(true);
  return await response.json() as InvitationResponse;
}

export async function approveWaitlistEntryByEmail(request: APIRequestContext, admin: E2eUser, email: string): Promise<void> {
  const login = await loginViaApi(request, admin);
  expect(login.tokens?.accessToken, `API login for ${admin.email} did not return an access token.`).toBeTruthy();

  const waitlistResponse = await request.get(`${e2eApiUrl}/auth/onboarding/waitlist`, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    }
  });

  expect(
    waitlistResponse.ok(),
    `Failed to load waitlist entries: ${waitlistResponse.status()} ${await waitlistResponse.text()}`
  ).toBe(true);

  const entries = await waitlistResponse.json() as WaitlistEntryResponse[];
  const entry = entries.find(item => item.email.toLowerCase() === email.toLowerCase());
  expect(entry, `Waitlist entry for ${email} was not found.`).toBeTruthy();

  const approveResponse = await request.post(`${e2eApiUrl}/auth/onboarding/waitlist/${entry!.waitlistEntryKey}/approve`, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    }
  });

  expect(
    approveResponse.ok(),
    `Failed to approve waitlist entry for ${email}: ${approveResponse.status()} ${await approveResponse.text()}`
  ).toBe(true);
}

export async function activateAccount(request: APIRequestContext, token: string, password: string): Promise<void> {
  const response = await request.post(`${e2eApiUrl}/auth/onboarding/activate`, {
    data: {
      token,
      password
    }
  });

  expect(response.ok(), `Account activation failed: ${response.status()} ${await response.text()}`).toBe(true);
}

export async function getSeededKurinKey(request: APIRequestContext, user: E2eUser): Promise<string> {
  const login = await loginViaApi(request, user);
  expect(login.kurinKey, `API login for ${user.email} did not return a kurinKey.`).toBeTruthy();
  return login.kurinKey!;
}

export async function getKurinByKey(request: APIRequestContext, user: E2eUser, kurinKey: string): Promise<KurinResponse> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();

  const response = await request.get(`${e2eApiUrl}/kurin/${kurinKey}`, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    }
  });

  expect(response.ok(), `Failed to load kurin ${kurinKey}: ${response.status()} ${await response.text()}`).toBe(true);
  return await response.json() as KurinResponse;
}

/**
 * Signs in and returns both the token and the kurins that account may act in. The two come from one
 * login on purpose: the suite then asserts against the same session the switcher would be using.
 */
export async function getKurinScopeOptions(
  request: APIRequestContext,
  user: E2eUser
): Promise<{ accessToken: string; memberKey: string; options: KurinScopeOption[] }> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();

  const accessToken = login.tokens!.accessToken!;
  const response = await request.get(`${e2eApiUrl}/auth/kurin-scope/options`, {
    headers: { Authorization: `Bearer ${accessToken}` }
  });

  expect(response.ok(), `Failed to read kurin scope options: ${response.status()} ${await response.text()}`).toBe(true);
  expect(login.memberKey, `API login for ${user.email} did not return a memberKey.`).toBeTruthy();
  return { accessToken, memberKey: login.memberKey!, options: await response.json() as KurinScopeOption[] };
}

/** Steps the session into one kurin and returns the roles the server hands back for it. */
export async function setKurinScopeViaApi(
  request: APIRequestContext,
  accessToken: string,
  kurinKey: string
): Promise<{ accessToken: string; roles: string[] }> {
  const response = await request.post(`${e2eApiUrl}/auth/kurin-scope`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: { kurinKey }
  });

  expect(response.ok(), `Failed to scope into ${kurinKey}: ${response.status()} ${await response.text()}`).toBe(true);
  const body = await response.json() as LoginResponse & { roles?: string[] };
  return { accessToken: body.tokens!.accessToken!, roles: body.roles ?? [] };
}

/** The "Членства" folder of someone's dossier — every kurin they belong to or have belonged to. */
export async function getMembershipsViaApi(
  request: APIRequestContext,
  accessToken: string,
  memberKey: string
): Promise<MembershipRecord[]> {
  const response = await request.get(`${e2eApiUrl}/member/${memberKey}/dossier/memberships`, {
    headers: { Authorization: `Bearer ${accessToken}` }
  });

  expect(response.ok(), `Failed to read memberships: ${response.status()} ${await response.text()}`).toBe(true);
  return await response.json() as MembershipRecord[];
}

export async function createGroupViaApi(
  request: APIRequestContext,
  user: E2eUser,
  kurinKey: string,
  name: string
): Promise<CreatedGroup> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();

  const response = await request.post(`${e2eApiUrl}/group`, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    },
    data: {
      groupKey: '00000000-0000-0000-0000-000000000000',
      kurinKey,
      name
    }
  });

  expect(response.ok(), `Failed to create group ${name}: ${response.status()} ${await response.text()}`).toBe(true);
  const group = await response.json() as GroupResponse;
  return { groupKey: group.groupKey, name: group.name };
}

export async function createMemberViaApi(
  request: APIRequestContext,
  user: E2eUser,
  scope: { groupKey: string } | { kurinKey: string },
  member: {
    firstName: string;
    middleName: string;
    lastName: string;
    email: string;
    createUserAccount?: boolean;
  }
): Promise<CreatedMember> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();

  const isGroupScope = 'groupKey' in scope;
  const url = isGroupScope
    ? `${e2eApiUrl}/member`
    : `${e2eApiUrl}/member/kurins/${scope.kurinKey}/members`;

  const multipart = new FormData();
  multipart.append('firstName', member.firstName);
  multipart.append('middleName', member.middleName);
  multipart.append('lastName', member.lastName);
  multipart.append('email', member.email);
  // The API takes phone numbers in international form only (+, country code, digits).
  multipart.append('phoneNumber', '+380501234567');
  multipart.append('dateOfBirth', '2000-01-01');
  multipart.append('createUserAccount', String(member.createUserAccount ?? false));

  if (isGroupScope) {
    multipart.append('groupKey', scope.groupKey);
  }

  const response = await request.post(url, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    },
    multipart
  });

  expect(response.ok(), `Failed to create member ${member.email}: ${response.status()} ${await response.text()}`).toBe(true);
  const savedMember = await response.json() as MemberResponse;

  return {
    memberKey: savedMember.memberKey,
    userKey: savedMember.userKey,
    email: member.email,
    firstName: member.firstName,
    middleName: member.middleName,
    lastName: member.lastName
  };
}

export async function getMembersByKurin(
  request: APIRequestContext,
  user: E2eUser,
  kurinKey: string
): Promise<MemberResponse[]> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();

  const response = await request.get(`${e2eApiUrl}/member/kurins/${kurinKey}/members`, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    }
  });

  expect(response.ok(), `Failed to load kurin members: ${response.status()} ${await response.text()}`).toBe(true);
  return await response.json() as MemberResponse[];
}

export async function getMembersByGroup(
  request: APIRequestContext,
  user: E2eUser,
  groupKey: string
): Promise<MemberResponse[]> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();

  const response = await request.get(`${e2eApiUrl}/member/groups/${groupKey}/members`, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    }
  });

  expect(response.ok(), `Failed to load group members: ${response.status()} ${await response.text()}`).toBe(true);
  return await response.json() as MemberResponse[];
}

export async function assignMentorViaApi(
  request: APIRequestContext,
  user: E2eUser,
  groupKey: string,
  mentorUserKey: string
): Promise<void> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();

  const response = await request.post(`${e2eApiUrl}/group/${groupKey}/mentors/${mentorUserKey}`, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    }
  });

  expect(response.ok(), `Failed to assign mentor ${mentorUserKey}: ${response.status()} ${await response.text()}`).toBe(true);
}

export async function createLeadershipViaApi(
  request: APIRequestContext,
  user: E2eUser,
  data: {
    type: 'Kurin' | 'Group' | 'KV';
    entityKey: string;
    role: string;
    memberKey: string;
    firstName: string;
    lastName: string;
    middleName?: string;
  }
): Promise<void> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();

  const response = await request.post(`${e2eApiUrl}/leadership`, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    },
    data: {
      type: data.type,
      entityKey: data.entityKey,
      startDate: new Date().toISOString().slice(0, 10),
      leadershipHistories: [
        {
          role: data.role,
          member: {
            memberKey: data.memberKey,
            firstName: data.firstName,
            lastName: data.lastName,
            middleName: data.middleName ?? ''
          },
          endDate: null
        }
      ]
    }
  });

  expect(response.ok(), `Failed to create ${data.type} leadership: ${response.status()} ${await response.text()}`).toBe(true);
}

export async function getSeededGroupKey(request: APIRequestContext, user: E2eUser, groupName: string): Promise<string> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();
  expect(login.kurinKey, `API login for ${user.email} did not return a kurinKey.`).toBeTruthy();

  const response = await request.get(`${e2eApiUrl}/group/groups`, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    },
    params: {
      kurinKey: login.kurinKey!
    }
  });

  expect(response.ok(), `Failed to load groups: ${response.status()} ${await response.text()}`).toBe(true);
  const groups = await response.json() as GroupResponse[];
  const group = groups.find(item => item.name === groupName);
  expect(group, `Seeded group ${groupName} was not found.`).toBeTruthy();

  return group!.groupKey;
}

export async function getFirstSeededGroupMemberKey(
  request: APIRequestContext,
  user: E2eUser,
  groupName = 'Gurtok 1'
): Promise<string> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();

  const groupKey = await getSeededGroupKey(request, user, groupName);
  const response = await request.get(`${e2eApiUrl}/member/groups/${groupKey}/members`, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    }
  });

  expect(response.ok(), `Failed to load group members: ${response.status()} ${await response.text()}`).toBe(true);
  const members = await response.json() as MemberResponse[];

  // Seeded members only, and always the same one. "First in the response" used to be enough until you
  // notice that manager-crud creates, edits and deletes a member in this very group while four other
  // specs read from it: whenever the new member happened to come back first, whoever picked it found
  // it deleted mid-test and landed on /forbidden. Seeded accounts live on @projectk.com; anything a
  // test makes for itself uses @example.com.
  const seeded = members
    .filter(item => !!item.memberKey && (item.email ?? '').endsWith('@projectk.com'))
    .sort((left, right) => (left.email ?? '').localeCompare(right.email ?? ''));

  expect(seeded[0], `Seeded group ${groupName} has no seeded members.`).toBeTruthy();

  return seeded[0].memberKey;
}

export async function getPlanningSessions(
  request: APIRequestContext,
  user: E2eUser,
  kurinKey?: string
): Promise<PlanningSessionResponse[]> {
  const login = await loginViaApi(request, user);
  expect(login.tokens?.accessToken, `API login for ${user.email} did not return an access token.`).toBeTruthy();
  const resolvedKurinKey = kurinKey ?? login.kurinKey;
  expect(resolvedKurinKey, `API login for ${user.email} did not return a kurinKey.`).toBeTruthy();

  const response = await request.get(`${e2eApiUrl}/planning/${resolvedKurinKey}`, {
    headers: {
      Authorization: `Bearer ${login.tokens!.accessToken}`
    }
  });

  expect(response.ok(), `Failed to load planning sessions: ${response.status()} ${await response.text()}`).toBe(true);
  return await response.json() as PlanningSessionResponse[];
}
