import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AuthService } from '../../../../../authModule/services/auth-service/auth.service';
import { KurinService } from '../../../../services/kurin-service/kurin.service';
import { PlastLevel } from '../../../../models/enums/plast-level.enum';
import { RosterImportComponent } from './import';
import { RosterField, RosterImportShape, RosterPreview } from './import-model';

const YOUTH: RosterImportShape = {
  required: [RosterField.LastName, RosterField.FirstName, RosterField.DateOfBirth, RosterField.PlastLevel, RosterField.PlastLevelDate, RosterField.GroupName, RosterField.Email],
  optional: [RosterField.MiddleName, RosterField.KurinNumber, RosterField.PhoneNumber],
  levelDates: [PlastLevel.Entry, PlastLevel.Uchasnyk],
  placesInGroups: true
};

const SENIOR: RosterImportShape = {
  ...YOUTH,
  required: YOUTH.required.filter(field => field !== RosterField.GroupName),
  levelDates: [PlastLevel.Starshoplastun],
  placesInGroups: false
};

describe('RosterImportComponent', () => {
  let fixture: ComponentFixture<RosterImportComponent>;
  let component: RosterImportComponent;
  let preview: RosterPreview;
  let sentGroups: boolean | null;
  const templateDownloads: string[] = [];

  beforeEach(() => {
    sentGroups = null;
    templateDownloads.length = 0;
    TestBed.configureTestingModule({
      imports: [RosterImportComponent],
      providers: [
        provideRouter([]),
        {
          provide: KurinService,
          useValue: {
            previewRoster: () => of(preview),
            importRoster: (_: string, request: { createMissingGroups: boolean }) => {
              sentGroups = request.createMissingGroups;
              return of({ rows: [], missingGroups: [], foreignKurinRows: [], dryRun: true, createdCount: 0, attachedCount: 0, alreadyHereCount: 0, rejectedCount: 0, invitedCount: 0 });
            },
            downloadImportTemplate: (kurinKey: string) => {
              templateDownloads.push(kurinKey);
              return of(new HttpResponse<Blob>({ body: null, headers: new HttpHeaders() }));
            }
          }
        },
        { provide: AuthService, useValue: { getAuthState: () => of({ kurinKey: 'kurin-1' }) } }
      ]
    });

    fixture = TestBed.createComponent(RosterImportComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  function upload(shape: RosterImportShape, columns: { header: string; field: RosterField }[]): void {
    preview = {
      shape,
      rows: [{ number: 2, cells: columns.map(() => 'x') }],
      columns: columns.map((column, index) => ({ index, header: column.header, sample: 'x', suggestedField: column.field, suggestedLevel: null }))
    };
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: [new File(['x'], 'roster.xlsx')] });
    component.onFileSelected({ target: input } as unknown as Event);
    fixture.detectChanges();
  }

  it('asks a senior kurin for no гурток and does not offer to open гуртки', () => {
    upload(SENIOR, [{ header: 'Гурток', field: RosterField.GroupName }]);

    expect(component.fieldOptions().some(option => option.field === RosterField.GroupName)).toBe(false);
    expect(component.unmappedRequiredNames()).not.toContain('Гурток');
    expect(component.chosenFor(0)).toBe(RosterField.Ignore);
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Створити гуртки');
  });

  it('never asks the server to open гуртки for a senior kurin', () => {
    upload(SENIOR, []);
    component.createMissingGroups.set(true);

    component.review();

    expect(sentGroups).toBe(false);
  });

  it('asks a youth kurin for its гурток and offers to open the missing ones', () => {
    upload(YOUTH, [{ header: 'Прізвище', field: RosterField.LastName }]);

    expect(component.unmappedRequiredNames()).toContain('Гурток');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Створити гуртки');
  });

  it('downloads the kurin\'s own template', () => {
    component.downloadTemplate();

    expect(templateDownloads).toEqual(['kurin-1']);
    expect(component.downloadingTemplate()).toBe(false);
  });
});
