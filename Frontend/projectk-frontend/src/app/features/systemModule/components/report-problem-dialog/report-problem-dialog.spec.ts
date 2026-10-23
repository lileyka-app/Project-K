import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ReportProblemDialogComponent } from './report-problem-dialog';
import { FeedbackService } from '../../services/feedback-service/feedback.service';
import { DeltaOp } from '../../functions/quill-delta-to-markdown.function';

/** Just enough Quill to hand the dialog contents and take an embed. */
function fakeQuill(ops: DeltaOp[]) {
  const root = document.createElement('div');
  return {
    root,
    ops,
    embeds: [] as string[],
    getContents: () => ({ ops }),
    getSelection: () => ({ index: 0, length: 0 }),
    getLength: () => 1,
    insertEmbed(_i: number, _t: string, value: string) { this.embeds.push(value); },
    insertText: () => undefined,
    setSelection: () => undefined,
    setContents() { this.ops = [{ insert: '\n' }]; },
    uploader: { options: { mimetypes: ['image/png', 'image/jpeg'] } as { mimetypes?: string[]; handler?: (range: unknown, files: File[]) => void } },
    getModule(name: string) {
      return name === 'uploader' ? this.uploader : { addHandler: () => undefined };
    }
  };
}

describe('ReportProblemDialogComponent', () => {
  let fixture: ComponentFixture<ReportProblemDialogComponent>;
  let component: ReportProblemDialogComponent;
  let feedback: MockedObject<FeedbackService>;

  beforeEach(async () => {
    feedback = {
      uploadScreenshot: vi.fn(),
      reportProblem: vi.fn()
    } as MockedObject<FeedbackService>;

    await TestBed.configureTestingModule({
      imports: [ReportProblemDialogComponent],
      providers: [provideRouter([]), { provide: FeedbackService, useValue: feedback }]
    }).compileComponents();

    fixture = TestBed.createComponent(ReportProblemDialogComponent);
    component = fixture.componentInstance;
    component.visible.set(true);
    fixture.detectChanges();
  });

  it('warns that the report is public before anything is typed', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('публічний');
    expect(text).toContain('Кроки, щоб відтворити');
    expect(text).toContain('Що мало статися');
  });

  it('keeps «Надіслати» off until something is written', () => {
    expect(component.hasDescription()).toBe(false);

    component.onEditorInit({ editor: fakeQuill([{ insert: 'Не працює\n' }]) as never });
    component.onTextChange();

    expect(component.hasDescription()).toBe(true);
  });

  it('sends Markdown, the route and the version, and shows the issue it became', () => {
    feedback.reportProblem.mockReturnValue(of({ issueUrl: 'https://github.com/x/y/issues/9', issueNumber: 9 }));
    component.onEditorInit({ editor: fakeQuill([{ insert: 'Кнопка ' }, { insert: 'не', attributes: { bold: true } }, { insert: ' працює\n' }]) as never });
    component.onTextChange();
    component.title.set('  Кнопка  ');
    component.steps.set('1. Натиснути');

    component.send();

    const payload = vi.mocked(feedback.reportProblem).mock.lastCall![0];
    expect(payload.description).toBe('Кнопка **не** працює');
    expect(payload.title).toBe('Кнопка');
    expect(payload.steps).toBe('1. Натиснути');
    expect(payload.expected).toBeNull();
    expect(payload.appVersion).toBeTruthy();
    expect(component.receipt()?.issueNumber).toBe(9);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('issue #9');
  });

  // A pasted or picked picture goes up first; only the address the server returns enters the text.
  it('uploads an attached picture and embeds the returned address', () => {
    feedback.uploadScreenshot.mockReturnValue(of({ url: 'https://blob/feedback-screenshots/a.png' }));
    const quill = fakeQuill([{ insert: '\n' }]);
    component.onEditorInit({ editor: quill as never });

    component.attach(new File(['x'], 'shot.png', { type: 'image/png' }));

    expect(component.screenshots()).toEqual(['https://blob/feedback-screenshots/a.png']);
    expect(quill.embeds).toEqual(['https://blob/feedback-screenshots/a.png']);
  });

  // Quill's clipboard and drop handlers pass image files to its uploader, which by default inlines
  // them as base64; here that path leads to the server, and WebP is welcome too.
  it('routes pictures Quill receives from paste or drop through the upload', () => {
    feedback.uploadScreenshot.mockReturnValue(of({ url: 'https://blob/feedback-screenshots/b.png' }));
    const quill = fakeQuill([{ insert: '\n' }]);
    component.onEditorInit({ editor: quill as never });

    quill.uploader.options.handler!({ index: 0, length: 0 }, [new File(['x'], 'shot.png', { type: 'image/png' })]);

    expect(feedback.uploadScreenshot).toHaveBeenCalledTimes(1);
    expect(quill.embeds).toEqual(['https://blob/feedback-screenshots/b.png']);
    expect(quill.uploader.options.mimetypes).toContain('image/webp');
  });

  it('explains a failed upload and a failed send instead of going quiet', () => {
    feedback.uploadScreenshot.mockReturnValue(throwError(() => new Error('boom')));
    component.attach(new File(['x'], 'shot.png', { type: 'image/png' }));
    expect(component.errorMessage()).toContain('скриншот');

    feedback.reportProblem.mockReturnValue(throwError(() => new Error('boom')));
    component.onEditorInit({ editor: fakeQuill([{ insert: 'щось\n' }]) as never });
    component.onTextChange();
    component.send();
    expect(component.errorMessage()).toContain('надіслати');
    expect(component.receipt()).toBeNull();
  });
});
