import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TermsOfService } from './terms-of-service';

describe('TermsOfService', () => {
  let component: TermsOfService;
  let fixture: ComponentFixture<TermsOfService>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TermsOfService],
      providers: [provideRouter([]), provideNoopAnimations()],
    }).compileComponents();

    fixture = TestBed.createComponent(TermsOfService);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display the title "Terms of Service"', () => {
    const titleEl = fixture.nativeElement.querySelector('mat-card-title');
    expect(titleEl.textContent).toContain('Terms of Service');
  });

  it('should render placeholder content sections', () => {
    const headings = fixture.nativeElement.querySelectorAll('h2');
    const headingTexts = Array.from(headings).map((h: any) => h.textContent);
    expect(headingTexts).toContain('1. Accettazione dei termini');
    expect(headingTexts).toContain('3. Account utente');
    expect(headingTexts).toContain('8. Legge applicabile');
  });
});
