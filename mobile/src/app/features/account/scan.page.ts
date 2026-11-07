import { Component, ElementRef, OnDestroy, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import {
  IonBackButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonItem,
  IonLabel,
  IonList,
  IonTitle,
  IonToolbar,
  ViewDidEnter,
  ViewWillLeave,
} from '@ionic/angular';
import { ScanTarget, scanTarget } from './scan-target';

interface Detector {
  detect(source: CanvasImageSource): Promise<{ rawValue: string }[]>;
}

declare global {
  interface Window {
    BarcodeDetector?: new (options: { formats: string[] }) => Detector;
  }
}

/**
 * «Сканувати QR»: the camera, read a few times a second, until a QR turns up. The phone's own reader
 * (`BarcodeDetector`) where there is one, jsQR where there is not. A Лілейка link is followed — a
 * transfer code into «Переходи», a card to its page; anything else is only shown as text.
 */
@Component({
  selector: 'app-scan',
  imports: [IonHeader, IonToolbar, IonButtons, IonBackButton, IonTitle, IonContent, IonList, IonItem, IonLabel],
  styles: `
    .viewfinder {
      aspect-ratio: 1;
      background: #000;
      border-radius: 16px;
      margin: 16px auto;
      max-width: 360px;
      overflow: hidden;
      width: calc(100% - 32px);
    }
    video {
      height: 100%;
      object-fit: cover;
      width: 100%;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/more" text="" /></ion-buttons>
        <ion-title>Сканувати QR</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content [fullscreen]="true">
      @if (foreign(); as text) {
        <ion-list [inset]="true" data-testid="foreign">
          <ion-item>
            <ion-label class="ion-text-wrap">
              <h2>Це не посилання Лілейки</h2>

              <p data-testid="foreign-text">{{ text }}</p>
            </ion-label>
          </ion-item>
          <ion-item [button]="true" [detail]="false" (click)="again()">
            <ion-label color="primary">Сканувати ще раз</ion-label>
          </ion-item>
        </ion-list>
      } @else if (problem(); as text) {
        <ion-list [inset]="true">
          <ion-item><ion-label class="ion-text-wrap"><p data-testid="camera-problem">{{ text }}</p></ion-label></ion-item>
        </ion-list>
      } @else {
        <div class="viewfinder"><video #video playsinline muted data-testid="viewfinder"></video></div>
        
      }
    </ion-content>
  `,
})
export class ScanPage implements ViewDidEnter, ViewWillLeave, OnDestroy {
  private readonly router = inject(Router);
  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');

  protected readonly foreign = signal<string | null>(null);
  protected readonly problem = signal<string | null>(null);

  private stream: MediaStream | null = null;
  private timer: ReturnType<typeof setInterval> | null = null;
  private readonly canvas = document.createElement('canvas');

  ionViewDidEnter(): void {
    void this.start();
  }

  ionViewWillLeave(): void {
    this.stop();
  }

  ngOnDestroy(): void {
    this.stop();
  }

  protected again(): void {
    this.foreign.set(null);
    setTimeout(() => void this.start());
  }

  private async start(): Promise<void> {
    if (!navigator.mediaDevices?.getUserMedia) {
      this.problem.set('Браузер не дає доступу до камери.');
      return;
    }

    try {
      this.stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
    } catch {
      this.problem.set('Немає доступу до камери.');
      return;
    }

    const video = this.video()?.nativeElement;
    if (!video) {
      this.stop();
      return;
    }
    video.srcObject = this.stream;
    await video.play().catch(() => undefined);

    const detect = await this.detector();
    this.timer = setInterval(() => void this.read(video, detect), 300);
  }

  private reading = false;

  private async read(video: HTMLVideoElement, detect: (frame: HTMLCanvasElement) => Promise<string | null>): Promise<void> {
    if (this.reading || video.readyState < 2) return;
    this.reading = true;
    try {
      this.canvas.width = video.videoWidth;
      this.canvas.height = video.videoHeight;
      this.canvas.getContext('2d', { willReadFrequently: true })?.drawImage(video, 0, 0);
      const text = await detect(this.canvas);
      if (text) this.follow(scanTarget(text, location.origin));
    } finally {
      this.reading = false;
    }
  }

  private follow(target: ScanTarget): void {
    this.stop();
    if (target.kind === 'transfer') {
      void this.router.navigate(['/tabs/kurin/transfers'], { queryParams: { code: target.code } });
    } else if (target.kind === 'card') {
      location.href = target.url;
    } else {
      this.foreign.set(target.text);
    }
  }

  /** The phone's own reader when it has one: faster and kinder to the battery than decoding in script. */
  private async detector(): Promise<(frame: HTMLCanvasElement) => Promise<string | null>> {
    if (window.BarcodeDetector) {
      const native = new window.BarcodeDetector({ formats: ['qr_code'] });
      return async (frame) => (await native.detect(frame).catch(() => []))[0]?.rawValue ?? null;
    }

    const { default: jsQR } = await import('jsqr');
    return async (frame) => {
      const context = frame.getContext('2d', { willReadFrequently: true });
      if (!context || !frame.width) return null;
      const image = context.getImageData(0, 0, frame.width, frame.height);
      return jsQR(image.data, image.width, image.height)?.data ?? null;
    };
  }

  private stop(): void {
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = null;
  }
}
