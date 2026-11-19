import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ImageCroppedEvent } from 'ngx-image-cropper';

import { ImageCropDialogComponent } from './image-crop-dialog';

describe('ImageCropDialogComponent', () => {
  let fixture: ComponentFixture<ImageCropDialogComponent>;
  let component: ImageCropDialogComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ImageCropDialogComponent],
      providers: [provideNoopAnimations()]
    }).compileComponents();

    fixture = TestBed.createComponent(ImageCropDialogComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('header', 'Емблема куреня');
    fixture.detectChanges();
  });

  it('previews the cropped area itself when the page gives no preview', () => {
    const blob = new Blob(['png'], { type: 'image/png' });

    component.onCropped({ blob } as ImageCroppedEvent);

    expect(component.shownPreview()).toMatch(/^blob:/);
  });

  it('shows the page preview over its own', () => {
    component.onCropped({ blob: new Blob(['png']) } as ImageCroppedEvent);
    fixture.componentRef.setInput('previewUrl', 'processed.png');

    expect(component.shownPreview()).toBe('processed.png');
  });

  it('saves the last cropped area, and nothing before a crop', () => {
    const saved: (Blob | null)[] = [];
    component.save.subscribe(blob => saved.push(blob));

    component.onSave();
    const blob = new Blob(['png']);
    component.onCropped({ blob } as ImageCroppedEvent);
    component.onSave();

    expect(saved).toEqual([null, blob]);
  });

  it('does not cancel while saving', () => {
    let cancelled = 0;
    component.cancelled.subscribe(() => cancelled++);
    fixture.componentRef.setInput('saving', true);

    component.onCancel();

    expect(cancelled).toBe(0);
  });
});
