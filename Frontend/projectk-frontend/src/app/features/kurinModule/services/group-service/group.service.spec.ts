import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { GroupService } from './group.service';
import { CreateGroupDto } from '../../models/requests/create-group.dto';
import { UpdateGroupDto } from '../../models/requests/update-group.dto';
import { GroupDto } from '../../models/group.dto';
import { environment } from '../../../../../environments/environment';
import { MemberLookupDto } from '../../models/requests/member/member-lookup.dto';

describe('GroupService', () => {
  let service: GroupService;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiUrl}/group`;

  const mockGroup: GroupDto = {
    groupKey: 'g1',
    name: 'Alpha',
    description: 'Group description',
    silhouetteUrl: 'group-silhouettes/2026/05/27/test.png',
    kurinKey: 'k1',
    kurinNumber: 10
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [GroupService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(GroupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getByKey sends GET to /group/:key', () => {
    service.getByKey('g1').subscribe(res => {
      expect(res).toEqual(mockGroup);
    });

    const req = httpMock.expectOne(`${baseUrl}/g1`);
    expect(req.request.method).toBe('GET');
    req.flush(mockGroup);
  });

  it('getAllByKurinKey sends GET with kurinKey param', () => {
    service.getAllByKurinKey('k1').subscribe(res => {
      expect(res).toEqual([mockGroup]);
    });

    const req = httpMock.expectOne(r =>
      r.url === `${baseUrl}/groups` && r.params.get('kurinKey') === 'k1'
    );
    expect(req.request.method).toBe('GET');
    req.flush([mockGroup]);
  });

  it('getAllByKurinKey reuses cached response within TTL', () => {
    service.getAllByKurinKey('k1').subscribe(res => {
      expect(res).toEqual([mockGroup]);
    });

    httpMock.expectOne(r =>
      r.url === `${baseUrl}/groups` && r.params.get('kurinKey') === 'k1'
    ).flush([mockGroup]);

    service.getAllByKurinKey('k1').subscribe(res => {
      expect(res).toEqual([mockGroup]);
    });

    httpMock.expectNone(r =>
      r.url === `${baseUrl}/groups` && r.params.get('kurinKey') === 'k1'
    );
  });

  it('create sends POST with body', () => {
    const dto: CreateGroupDto = { name: 'Alpha', kurinKey: 'k1', description: 'Group description' };

    service.create(dto).subscribe(res => {
      expect(res).toEqual(mockGroup);
    });

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(dto);
    req.flush(mockGroup);
  });

  it('create invalidates cached group list', () => {
    const dto: CreateGroupDto = { name: 'Alpha', kurinKey: 'k1', description: 'Group description' };

    service.getAllByKurinKey('k1').subscribe();
    httpMock.expectOne(r =>
      r.url === `${baseUrl}/groups` && r.params.get('kurinKey') === 'k1'
    ).flush([mockGroup]);

    service.create(dto).subscribe();
    httpMock.expectOne(baseUrl).flush(mockGroup);

    service.getAllByKurinKey('k1').subscribe();
    const req = httpMock.expectOne(r =>
      r.url === `${baseUrl}/groups` && r.params.get('kurinKey') === 'k1'
    );
    expect(req.request.method).toBe('GET');
    req.flush([mockGroup]);
  });

  it('update sends PUT with body to /group/:key', () => {
    const updateDto: UpdateGroupDto = { name: 'Alpha Updated', description: 'Updated description' };

    service.update('g1', updateDto).subscribe(res => {
      expect(res).toEqual({ ...mockGroup, name: 'Alpha Updated', description: 'Updated description' });
    });

    const req = httpMock.expectOne(`${baseUrl}/g1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(updateDto);
    req.flush({ ...mockGroup, name: 'Alpha Updated', description: 'Updated description' });
  });

  it('delete sends DELETE to /group/:key', () => {
    service.delete('g1').subscribe(res => {
      expect(res).toBeNull();
    });

    const req = httpMock.expectOne(`${baseUrl}/g1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('uploadSilhouette sends multipart POST to /group/:key/silhouette', () => {
    const file = new File(['image'], 'silhouette.png', { type: 'image/png' });

    service.uploadSilhouette('g1', file).subscribe(res => {
      expect(res).toEqual(mockGroup);
    });

    const req = httpMock.expectOne(`${baseUrl}/g1/silhouette`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    expect((req.request.body as FormData).get('file')).toBe(file);
    req.flush(mockGroup);
  });

  it('deleteSilhouette sends DELETE to /group/:key/silhouette', () => {
    service.deleteSilhouette('g1').subscribe(res => {
      expect(res).toEqual({ ...mockGroup, silhouetteUrl: null });
    });

    const req = httpMock.expectOne(`${baseUrl}/g1/silhouette`);
    expect(req.request.method).toBe('DELETE');
    req.flush({ ...mockGroup, silhouetteUrl: null });
  });

  it('getMentors sends GET to /group/:key/mentors', () => {
    const mentors: MemberLookupDto[] = [
      { memberKey: 'm1', userKey: 'u1', firstName: 'A', middleName: 'B', lastName: 'C' }
    ];

    service.getMentors('g1').subscribe(res => {
      expect(res).toEqual(mentors);
    });

    const req = httpMock.expectOne(`${baseUrl}/g1/mentors`);
    expect(req.request.method).toBe('GET');
    req.flush(mentors);
  });

  it('assignMentor sends POST to /group/:key/mentors/:mentorUserKey', () => {
    service.assignMentor('g1', 'u1').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/g1/mentors/u1`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    req.flush({});
  });

  it('revokeMentor sends DELETE to /group/:key/mentors/:mentorUserKey', () => {
    service.revokeMentor('g1', 'u1').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/g1/mentors/u1`);
    expect(req.request.method).toBe('DELETE');
    req.flush({});
  });
});

