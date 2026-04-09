import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Login } from './login';
import { AuthService } from 'shared-auth';

describe('Login', () => {
  let component: Login;
  let fixture: ComponentFixture<Login>;
  let authService: {
    login: ReturnType<typeof vi.fn>;
    isAuthenticated: any;
    currentUser: any;
    accessToken: any;
    mustChangePassword: any;
    consentUpdateRequired: any;
    acceptUpdatedConsent: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };
  let dialog: { open: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    authService = {
      login: vi.fn(),
      isAuthenticated: signal(false),
      currentUser: signal(null),
      accessToken: signal(null),
      mustChangePassword: signal(false),
      consentUpdateRequired: signal(false),
      acceptUpdatedConsent: vi.fn(),
      logout: vi.fn(),
    };
    dialog = { open: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AuthService, useValue: authService },
        { provide: MatDialog, useValue: dialog },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have invalid form initially', () => {
    expect(component['form'].invalid).toBe(true);
  });

  it('should call AuthService.login on valid submit', () => {
    const mockResponse = {
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2026-12-31',
      user: { id: '1', email: 'test@example.com', firstName: 'John', lastName: 'Doe' },
    };
    authService.login.mockReturnValue(of(mockResponse));

    component['form'].setValue({ email: 'test@example.com', password: 'Password1' });
    component.onSubmit();

    expect(authService.login).toHaveBeenCalledWith({
      email: 'test@example.com',
      password: 'Password1',
    });
  });

  it('should not call AuthService.login when form is invalid', () => {
    component.onSubmit();
    expect(authService.login).not.toHaveBeenCalled();
  });

  it('should set error on login failure', () => {
    authService.login.mockReturnValue(
      throwError(() => ({ error: { errors: ['Invalid credentials'] } })),
    );

    component['form'].setValue({ email: 'test@example.com', password: 'Password1' });
    component.onSubmit();

    expect(component['error']()).toBe('Invalid credentials');
    expect(component['loading']()).toBe(false);
  });

  it('should open consent dialog when consentUpdateRequired is true', () => {
    const consentSignal = signal(true);
    authService.consentUpdateRequired = consentSignal;
    authService.login.mockReturnValue(of({ accessToken: 'token' }));
    dialog.open.mockReturnValue({ afterClosed: () => of(true) });
    authService.acceptUpdatedConsent.mockReturnValue(of(undefined));

    component['form'].setValue({ email: 'test@example.com', password: 'Password1' });
    component.onSubmit();

    expect(dialog.open).toHaveBeenCalled();
  });
});
