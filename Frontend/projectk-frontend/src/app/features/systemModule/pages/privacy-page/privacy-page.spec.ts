import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PrivacyPageComponent } from './privacy-page';

describe('PrivacyPageComponent', () => {
  let fixture: ComponentFixture<PrivacyPageComponent>;
  let component: PrivacyPageComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PrivacyPageComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(PrivacyPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('names who is responsible, the law, and the revision date', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Ростислав Муха');
    expect(text).toContain('Про захист персональних даних');
    expect(text).toContain(component.revisedOn);
  });

  // The table is the policy's substance: every stored kind of data must say why and who enters it.
  it('lists every kind of stored data with a purpose and a source', () => {
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('.privacy-table tbody tr');

    expect(rows).toHaveLength(component.dataRows.length);
    expect(component.dataRows.every(r => r.what && r.why && r.who)).toBe(true);
    expect(component.dataRows.some(r => /дата народження/i.test(r.what))).toBe(true);
  });

  it('tells minors’ data is entered on parental consent and how to have it removed', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('згоди батьків');
    expect(text).toContain('Відкликати згоду');
    expect(text).toContain('30 днів');
  });
});
