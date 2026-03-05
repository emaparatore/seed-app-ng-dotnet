import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { Profile } from './profile';
import { AuthService } from 'shared-auth';

describe('Profile', () => {
  let component: Profile;
  let fixture: ComponentFixture<Profile>;
  let authService: { getProfile: ReturnType<typeof vi.fn>; currentUser: any; isAuthenticated: any; accessToken: any };
  const mockUser = { id: '1', email: 'test@example.com', firstName: 'John', lastName: 'Doe' };

  beforeEach(async () => {
    authService = {
      getProfile: vi.fn().mockReturnValue(of(mockUser)),
      currentUser: signal(mockUser),
      isAuthenticated: signal(true),
      accessToken: signal('test-token'),
    };

    await TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AuthService, useValue: authService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Profile);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should call getProfile on init', () => {
    component.ngOnInit();
    expect(authService.getProfile).toHaveBeenCalled();
  });

  it('should set loading to false after profile loads', () => {
    component.ngOnInit();
    expect(component['loading']()).toBe(false);
  });

  it('should set error on profile load failure', () => {
    authService.getProfile.mockReturnValue(throwError(() => new Error('fail')));
    component.ngOnInit();
    expect(component['error']()).toBe('Unable to load user details.');
    expect(component['loading']()).toBe(false);
  });
});
