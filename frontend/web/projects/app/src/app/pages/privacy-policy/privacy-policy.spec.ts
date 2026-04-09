import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { PrivacyPolicy } from './privacy-policy';

describe('PrivacyPolicy', () => {
  let component: PrivacyPolicy;
  let fixture: ComponentFixture<PrivacyPolicy>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PrivacyPolicy],
      providers: [provideRouter([]), provideNoopAnimations()],
    }).compileComponents();

    fixture = TestBed.createComponent(PrivacyPolicy);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display the title "Privacy Policy"', () => {
    const titleEl = fixture.nativeElement.querySelector('mat-card-title');
    expect(titleEl.textContent).toContain('Privacy Policy');
  });

  it('should render placeholder content sections', () => {
    const headings = fixture.nativeElement.querySelectorAll('h2');
    const headingTexts = Array.from(headings).map((h: any) => h.textContent);
    expect(headingTexts).toContain('1. Titolare del trattamento');
    expect(headingTexts).toContain('2. Dati raccolti');
    expect(headingTexts).toContain('6. Diritti dell\'interessato');
  });
});
