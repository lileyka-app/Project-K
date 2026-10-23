import { TestBed } from '@angular/core/testing';
import { UserActionService } from './user-action.service';

describe('UserActionService', () => {
  let service: UserActionService;
  let button: HTMLButtonElement;
  let clicks: number;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({});
    service = TestBed.inject(UserActionService);
    service.start();

    button = document.createElement('button');
    button.className = 'p-button';
    button.innerHTML = '<span class="p-button-icon pi pi-save"></span><span class="p-button-label">Зберегти</span>';
    clicks = 0;
    button.addEventListener('click', () => clicks++);
    document.body.appendChild(button);
  });

  afterEach(() => {
    button.remove();
    vi.useRealTimers();
  });

  it('knows nothing about a request that no gesture started', () => {
    expect(service.claim()).toBeNull();
  });

  it('spins the clicked button until the request is released', () => {
    button.click();
    const claim = service.claim();

    expect(claim?.action.button).toBe(button);
    expect(button.classList).toContain('lil-busy');
    expect(button.getAttribute('aria-busy')).toBe('true');
    expect(button.querySelector('.lil-busy-icon')).not.toBeNull();

    claim!.release();
    expect(button.classList).not.toContain('lil-busy');
    expect(button.querySelector('.lil-busy-icon')).toBeNull();
    vi.advanceTimersByTime(0);
  });

  it('swallows a second press while the first is still in flight', () => {
    button.click();
    const claim = service.claim();
    button.click();
    button.click();

    expect(clicks).toBe(1);

    claim!.release();
    button.click();
    expect(clicks).toBe(2);
    vi.advanceTimersByTime(0);
  });

  it('keeps spinning while any request of the action is still out', () => {
    button.click();
    const first = service.claim()!;
    const second = service.claim()!;

    first.release();
    expect(button.classList).toContain('lil-busy');
    second.release();
    expect(button.classList).not.toContain('lil-busy');
    vi.advanceTimersByTime(0);
  });

  it('forgets the gesture once the task that carried it is over', () => {
    button.click();
    vi.advanceTimersByTime(0);

    expect(service.claim()).toBeNull();
  });

  it('hands the action on to the request a response starts', () => {
    button.click();
    const claim = service.claim()!;
    vi.advanceTimersByTime(0);

    service.resume(claim.action);
    expect(service.claim()?.action).toBe(claim.action);
    vi.advanceTimersByTime(0);
  });
});
