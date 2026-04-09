import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppFooter } from './footer';

describe('AppFooter', () => {
  let component: AppFooter;
  let fixture: ComponentFixture<AppFooter>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppFooter],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(AppFooter);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have a link to /privacy-policy', () => {
    const links = fixture.nativeElement.querySelectorAll('a');
    const privacyLink = Array.from(links).find(
      (a: any) => a.getAttribute('href') === '/privacy-policy',
    );
    expect(privacyLink).toBeTruthy();
  });

  it('should have a link to /terms-of-service', () => {
    const links = fixture.nativeElement.querySelectorAll('a');
    const tosLink = Array.from(links).find(
      (a: any) => a.getAttribute('href') === '/terms-of-service',
    );
    expect(tosLink).toBeTruthy();
  });
});
