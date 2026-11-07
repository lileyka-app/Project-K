import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, Subject, throwError } from 'rxjs';
import { MockedObject } from 'vitest';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { MemberProgressService } from '../../services/probes-and-badges/member-progress.service';
import { ThirdProbeDto } from '../../models/probes-and-badges/probe-project.dto';
import { probeProject, thirdProbeTab } from '../../functions/third-probe.fixtures';
import { ProbeProjectsComponent } from './probe-projects';

const IMAGE_KEY = '3f2504e0-4f89-11d3-9a0c-0305e82c3301';

describe('ProbeProjectsComponent', () => {
  let fixture: ComponentFixture<ProbeProjectsComponent>;
  let progress: MockedObject<MemberProgressService>;
  let emitted: ThirdProbeDto[];

  async function render(tab: ThirdProbeDto, readOnly = false): Promise<HTMLElement> {
    fixture = TestBed.createComponent(ProbeProjectsComponent);
    fixture.componentRef.setInput('memberKey', 'm-1');
    fixture.componentRef.setInput('probeId', 'upyu-2026-3');
    fixture.componentRef.setInput('tab', tab);
    fixture.componentRef.setInput('readOnly', readOnly);
    emitted = [];
    fixture.componentInstance.tabChange.subscribe(next => emitted.push(next));
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(async () => {
    progress = {
      addProbeProject: vi.fn(),
      updateProbeProject: vi.fn(),
      deleteProbeProject: vi.fn(),
      swapSpareProject: vi.fn(),
      signProbeProject: vi.fn(),
      uploadProbeProjectImage: vi.fn()
    } as MockedObject<MemberProgressService>;

    await TestBed.configureTestingModule({
      imports: [ProbeProjectsComponent],
      providers: [{ provide: MemberProgressService, useValue: progress }, ConfirmationService]
    }).compileComponents();
  });

  it('shows a project as the template lays it out, with its own picture', async () => {
    const page = await render(thirdProbeTab({
      projects: [probeProject({
        results: `Дійшли.\n\n![зображення](project-image:${IMAGE_KEY})`,
        images: [{ imageKey: IMAGE_KEY, url: 'https://blob/probe-project-images/a.jpg?sig' }],
        completedAtUtc: '2026-11-10T00:00:00Z', completedByName: 'Звʼязковий', coordinatorComment: 'Рекомендую'
      })]
    }));

    expect(page.textContent).toContain('Г. Життя в природі');
    expect(page.textContent).toContain('Олена Коваль');
    expect(page.textContent).toContain('Підсумки виконання');
    expect(page.textContent).toContain('«Рекомендую» — координатор');
    expect(page.querySelector<HTMLImageElement>('.md__image')?.src).toContain('probe-project-images/a.jpg');
  });

  it('shows the spare apart, and offers to swap it in', async () => {
    const page = await render(thirdProbeTab({
      projects: [probeProject(), probeProject({ projectKey: 'spare', title: 'Ватра', isSpare: true })]
    }));

    expect(page.textContent).toContain('Запасний проєкт');
    expect(page.textContent).toContain('Поставити замість');
  });

  it('gives leaders the confirmations and youths none', async () => {
    const youth = await render(thirdProbeTab({ projects: [probeProject()] }));
    expect(youth.textContent).not.toContain('Дати дозвіл');

    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [ProbeProjectsComponent],
      providers: [{ provide: MemberProgressService, useValue: progress }, ConfirmationService]
    }).compileComponents();
    const leader = await render(thirdProbeTab({ canSign: true, projects: [probeProject()] }));
    expect(leader.textContent).toContain('Дати дозвіл');
  });

  it('passes the tab up after a leader confirms', async () => {
    await render(thirdProbeTab({ canSign: true, projects: [probeProject()] }));
    const after = thirdProbeTab({ projects: [probeProject({ approvedAtUtc: '2026-10-30T00:00:00Z' })] });
    progress.signProbeProject.mockReturnValue(of(after));

    fixture.componentInstance.approve(probeProject(), true);

    expect(progress.signProbeProject).toHaveBeenCalledWith('m-1', 'upyu-2026-3', 'p-1', 'approval', true);
    expect(emitted).toEqual([after]);
  });

  it('offers nothing to write on an archive', async () => {
    const page = await render(thirdProbeTab({ canSign: true, projects: [probeProject()] }), true);

    expect(page.textContent).not.toContain('Редагувати');
    expect(page.textContent).not.toContain('Дати дозвіл');
  });

  it('offers the spare once five main projects are there, and nothing after it', async () => {
    const five = Array.from({ length: 5 }, (_, i) => probeProject({ projectKey: `p-${i}` }));
    expect((await render(thirdProbeTab({ projects: five }))).textContent).toContain('Додати запасний');

    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [ProbeProjectsComponent],
      providers: [{ provide: MemberProgressService, useValue: progress }, ConfirmationService]
    }).compileComponents();
    const six = [...five, probeProject({ projectKey: 'spare', isSpare: true })];
    expect((await render(thirdProbeTab({ projects: six }))).textContent).not.toContain('Додати');
  });

  it('says in words why the server refused a save', async () => {
    await render(thirdProbeTab());
    const component = fixture.componentInstance;
    progress.addProbeProject.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'TooManyShared' } })));

    component.openNew();
    component.title.set('Третій спільний');
    component.save();

    expect(component.errorMessage()).toBe('Спільних з іншими третьопробниками може бути лише два з пʼяти основних.');
  });

  it('saves a new project once for several pictures dropped together', async () => {
    await render(thirdProbeTab());
    const component = fixture.componentInstance;
    const created = new Subject<{ projectKey: string; tab: ThirdProbeDto }>();
    progress.addProbeProject.mockReturnValue(created as never);
    progress.uploadProbeProjectImage.mockReturnValue(of({ imageKey: IMAGE_KEY, url: 'https://blob/a.jpg?sig' }));
    const attach = (component as unknown as { attach(file: File, field: string): void }).attach.bind(component);

    component.openNew();
    component.title.set('Похід');
    attach(new File([new Uint8Array(10)], 'a.png', { type: 'image/png' }), 'goal');
    attach(new File([new Uint8Array(10)], 'b.png', { type: 'image/png' }), 'results');
    expect(component.isUploading()).toBe(true);
    created.next({ projectKey: 'p-new', tab: thirdProbeTab() });
    created.complete();

    expect(progress.addProbeProject).toHaveBeenCalledTimes(1);
    expect(progress.uploadProbeProjectImage).toHaveBeenCalledTimes(2);
    expect(progress.uploadProbeProjectImage.mock.calls.map(call => call[2])).toEqual(['p-new', 'p-new']);
    expect(component.isUploading()).toBe(false);
  });
});
