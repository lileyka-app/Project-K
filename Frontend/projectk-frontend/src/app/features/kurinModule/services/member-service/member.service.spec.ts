import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { MemberService } from './member.service';
import { environment } from '../../../../../environments/environment';
import { UpsertMemberDto } from '../../models/requests/member/upsert-member.dto';
import { MemberDto } from '../../models/member.dto';
import { MemberLookupDto } from '../../models/requests/member/member-lookup.dto';
import { MemberProfileVerificationStatus } from '../../models/enums/member-profile-verification-status.enum';

describe('MemberService', () => {
  let service: MemberService;
  let httpMock: HttpTestingController;

  const apiUrl = `${environment.apiUrl}/member`;

  const sampleUpsert: UpsertMemberDto = {
    groupKey: 'group-1',
    firstName: 'John',
    middleName: 'M',
    lastName: 'Doe',
    email: 'john@example.com',
    phoneNumber: '123456789',
    dateOfBirth: '2000-05-10'
  };

  const sampleMember: MemberDto = {
    memberKey: 'member-key-1',
    groupKey: 'group-1',
    kurinKey: 'kurin-1',
    firstName: 'John',
    middleName: 'M',
    lastName: 'Doe',
    email: 'john@example.com',
    phoneNumber: '123456789',
    dateOfBirth: null,
    profilePhotoUrl: null,
    plastLevelHistories: [],
    leadershipHistories: []
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        MemberService
      ]
    });
    service = TestBed.inject(MemberService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getByKey should call correct URL and return MemberDto', () => {
    const id = '123';
    service.getByKey(id).subscribe(res => {
      expect(res).toEqual(sampleMember);
    });

    const req = httpMock.expectOne(`${apiUrl}/${id}`);
    expect(req.request.method).toBe('GET');
    req.flush(sampleMember);
  });

  it('getAll should call group members endpoint when groupKey is provided', () => {
    const groupKey = 'g1';

    service.getAll(groupKey).subscribe(res => {
      expect(res).toHaveSize(1);
    });

    const req = httpMock.expectOne(`${apiUrl}/groups/${groupKey}/members`);
    expect(req).toBeTruthy();
    expect(req.request.method).toBe('GET');
    req.flush([sampleMember]);
  });

  it('getAll should reuse cached group members within TTL', () => {
    const groupKey = 'g1';
    let firstResponse: MemberDto[] | undefined;
    let secondResponse: MemberDto[] | undefined;

    service.getAll(groupKey).subscribe(res => firstResponse = res);
    service.getAll(groupKey).subscribe(res => secondResponse = res);

    const req = httpMock.expectOne(`${apiUrl}/groups/${groupKey}/members`);
    expect(req.request.method).toBe('GET');
    req.flush([sampleMember]);

    expect(firstResponse).toEqual([sampleMember]);
    expect(secondResponse).toEqual([sampleMember]);

    service.getAll(groupKey).subscribe(res => {
      expect(res).toEqual([sampleMember]);
    });

    httpMock.expectNone(`${apiUrl}/groups/${groupKey}/members`);
  });

  it('update should invalidate member list cache', () => {
    const groupKey = 'g1';

    service.getAll(groupKey).subscribe();
    httpMock.expectOne(`${apiUrl}/groups/${groupKey}/members`).flush([sampleMember]);

    service.update(sampleMember.memberKey, sampleUpsert, null).subscribe();
    httpMock.expectOne(`${apiUrl}/${sampleMember.memberKey}`).flush(sampleMember);

    service.getAll(groupKey).subscribe();
    const req = httpMock.expectOne(`${apiUrl}/groups/${groupKey}/members`);
    expect(req.request.method).toBe('GET');
    req.flush([sampleMember]);
  });

  it('getAll should call kurin members endpoint when kurinKey is provided', () => {
    const kurinKey = 'k1';

    service.getAll(undefined, kurinKey).subscribe(res => {
      expect(res).toHaveSize(1);
    });

    const req = httpMock.expectOne(`${apiUrl}/kurins/${kurinKey}/members`);
    expect(req.request.method).toBe('GET');
    req.flush([sampleMember]);
  });

  it('getAll should fail when both groupKey and kurinKey are missing', () => {
    let receivedError: unknown;

    service.getAll().subscribe({
      error: (error) => {
        receivedError = error;
      }
    });

    expect(receivedError).toEqual(jasmine.any(Error));
    expect((receivedError as Error).message).toContain('Either groupKey or kurinKey must be provided.');
  });

  it('create should POST FormData with individual dto properties and blob when file provided', () => {
    const file = new File(['content'], 'avatar.png', { type: 'image/png' });

    service.create(sampleUpsert, file).subscribe(res => {
      expect(res).toEqual(sampleMember);
    });

    const req = httpMock.expectOne(apiUrl);
    expect(req.request.method).toBe('POST');
    const body = req.request.body as FormData;
    expect(body instanceof FormData).toBeTrue();

    // Check all DTO properties are present in FormData
    Object.entries(sampleUpsert).forEach(([key, value]) => {
      if (value !== null && value !== undefined) {
        expect(body.get(key)).toEqual(value.toString());
      }
    });

    // Verify blob is included with filename
    const blobEntry = body.get('blob') as File;
    expect(blobEntry).toBeTruthy();
    expect(blobEntry.name).toBe('avatar.png');

    req.flush(sampleMember);
  });

  it('create should handle Date objects by converting date-only fields to yyyy-MM-dd', () => {
    const dateUpsert: UpsertMemberDto = {
      ...sampleUpsert,
      dateOfBirth: new Date(2000, 4, 10) as unknown as string
    };
    
    service.create(dateUpsert, null).subscribe();

    const req = httpMock.expectOne(apiUrl);
    const body = req.request.body as FormData;
    
    expect(body.get('dateOfBirth')).toEqual('2000-05-10');
    req.flush(sampleMember);
  });

  it('create should POST FormData without blob when file is null', () => {
    service.create(sampleUpsert, null).subscribe(res => {
      expect(res).toEqual(sampleMember);
    });

    const req = httpMock.expectOne(apiUrl);
    expect(req.request.method).toBe('POST');
    const body = req.request.body as FormData;

    let hasBlob = false;
    body.forEach((_v: FormDataEntryValue, key: string) => {
      if (key === 'blob') hasBlob = true;
    });
    expect(hasBlob).toBeFalse();

    req.flush(sampleMember);
  });

  it('create should POST to kurin endpoint when kurinKey is provided', () => {
    const request: UpsertMemberDto = {
      ...sampleUpsert,
      groupKey: undefined,
      kurinKey: 'kurin-42'
    };

    service.create(request, null).subscribe(res => {
      expect(res).toEqual(sampleMember);
    });

    const req = httpMock.expectOne(`${apiUrl}/kurins/kurin-42/members`);
    expect(req.request.method).toBe('POST');
    req.flush(sampleMember);
  });

  it('create should fail when both groupKey and kurinKey are missing', () => {
    let receivedError: unknown;

    service.create({
      ...sampleUpsert,
      groupKey: undefined,
      kurinKey: undefined
    }, null).subscribe({
      error: (error) => {
        receivedError = error;
      }
    });

    expect(receivedError).toEqual(jasmine.any(Error));
    expect((receivedError as Error).message).toContain('Either groupKey or kurinKey must be provided for create.');
  });

  it('update should PUT FormData to correct URL with individual properties and file', () => {
    const memberKey = 'member-key-1';
    const file = new File(['xx'], 'photo.jpg', { type: 'image/jpeg' });

    service.update(memberKey, sampleUpsert, file).subscribe(res => {
      expect(res).toEqual(sampleMember);
    });

    const req = httpMock.expectOne(`${apiUrl}/${memberKey}`);
    expect(req.request.method).toBe('PUT');
    const body = req.request.body as FormData;

    // Check DTO properties
    Object.entries(sampleUpsert).forEach(([key, value]) => {
      if (value !== null && value !== undefined) {
        expect(body.get(key)).toEqual(value.toString());
      }
    });

    // Check blob with filename
    const blobEntry = body.get('blob') as File;
    expect(blobEntry).toBeTruthy();
    expect(blobEntry.name).toBe('photo.jpg');

    req.flush(sampleMember);
  });

  it('update should omit blob when file null', () => {
    const memberKey = 'member-key-2';

    service.update(memberKey, sampleUpsert, null).subscribe(res => {
      expect(res).toEqual(sampleMember);
    });

    const req = httpMock.expectOne(`${apiUrl}/${memberKey}`);
    expect(req.request.method).toBe('PUT');
    const body = req.request.body as FormData;

    let containsBlob = false;
    body.forEach((_v: FormDataEntryValue, key: string) => {
      if (key === 'blob') containsBlob = true;
    });
    expect(containsBlob).toBeFalse();

    req.flush(sampleMember);
  });

  it('delete should call correct URL', () => {
    const memberKey = 'member-key-del';

    service.delete(memberKey).subscribe(res => {
      expect(res).toBeNull();
    });

    const req = httpMock.expectOne(`${apiUrl}/${memberKey}`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('verifyProfile should PUT note and invalidate member cache', () => {
    const groupKey = 'g1';
    const verifiedMember: MemberDto = {
      ...sampleMember,
      profileVerificationStatus: MemberProfileVerificationStatus.VerifiedCurrent,
      profileVerificationNote: 'Checked'
    };

    service.getAll(groupKey).subscribe();
    httpMock.expectOne(`${apiUrl}/groups/${groupKey}/members`).flush([sampleMember]);

    service.verifyProfile(sampleMember.memberKey, 'Checked').subscribe(res => {
      expect(res.profileVerificationStatus).toBe(MemberProfileVerificationStatus.VerifiedCurrent);
      expect(res.profileVerificationNote).toBe('Checked');
    });

    const verifyReq = httpMock.expectOne(`${apiUrl}/${sampleMember.memberKey}/profile-verification`);
    expect(verifyReq.request.method).toBe('PUT');
    expect(verifyReq.request.body).toEqual({ note: 'Checked' });
    verifyReq.flush(verifiedMember);

    service.getAll(groupKey).subscribe();
    const listReq = httpMock.expectOne(`${apiUrl}/groups/${groupKey}/members`);
    expect(listReq.request.method).toBe('GET');
    listReq.flush([verifiedMember]);
  });

  it('resetProfileVerification should DELETE and invalidate member cache', () => {
    const groupKey = 'g1';
    const resetMember: MemberDto = {
      ...sampleMember,
      profileVerificationStatus: MemberProfileVerificationStatus.Unverified,
      profileVerificationNote: null
    };

    service.getAll(groupKey).subscribe();
    httpMock.expectOne(`${apiUrl}/groups/${groupKey}/members`).flush([sampleMember]);

    service.resetProfileVerification(sampleMember.memberKey).subscribe(res => {
      expect(res.profileVerificationStatus).toBe(MemberProfileVerificationStatus.Unverified);
      expect(res.profileVerificationNote).toBeNull();
    });

    const resetReq = httpMock.expectOne(`${apiUrl}/${sampleMember.memberKey}/profile-verification`);
    expect(resetReq.request.method).toBe('DELETE');
    resetReq.flush(resetMember);

    service.getAll(groupKey).subscribe();
    const listReq = httpMock.expectOne(`${apiUrl}/groups/${groupKey}/members`);
    expect(listReq.request.method).toBe('GET');
    listReq.flush([resetMember]);
  });

  it('getMentorCandidates should call mentor-candidates endpoint', () => {
    service.getMentorCandidates('k-1').subscribe(res => {
      expect(res).toHaveSize(1);
      expect(res[0].userKey).toBe('u1');
    });

    const req = httpMock.expectOne(`${apiUrl}/members/mentor-candidates/k-1`);
    expect(req.request.method).toBe('GET');
    req.flush([{ memberKey: 'm1', userKey: 'u1', firstName: 'Mentor', middleName: 'M', lastName: 'One' }]);
  });

  it('getMentorCandidates should reuse cached response within TTL', () => {
    const candidates: MemberLookupDto[] = [
      { memberKey: 'm1', userKey: 'u1', firstName: 'Mentor', middleName: 'M', lastName: 'One' }
    ];

    service.getMentorCandidates('k-1').subscribe(res => {
      expect(res).toEqual(candidates);
    });

    httpMock.expectOne(`${apiUrl}/members/mentor-candidates/k-1`).flush(candidates);

    service.getMentorCandidates('k-1').subscribe(res => {
      expect(res).toEqual(candidates);
    });

    httpMock.expectNone(`${apiUrl}/members/mentor-candidates/k-1`);
  });
});

