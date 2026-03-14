import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { Register } from './register';
import { AuthService } from 'shared-auth';

describe('Register', () => {
  let component: Register;
  let fixture: ComponentFixture<Register>;
  let authService: { register: ReturnType<typeof vi.fn>; isAuthenticated: any; currentUser: any; accessToken: any };

  beforeEach(async () => {
    authService = {
      register: vi.fn(),
      isAuthenticated: signal(false),
      currentUser: signal(null),
      accessToken: signal(null),
    };

    await TestBed.configureTestingModule({
      imports: [Register],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AuthService, useValue: authService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Register);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have invalid form initially', () => {
    expect(component['form'].invalid).toBe(true);
  });

  it('should validate required fields', () => {
    const form = component['form'];
    expect(form.controls.firstName.hasError('required')).toBe(true);
    expect(form.controls.lastName.hasError('required')).toBe(true);
    expect(form.controls.email.hasError('required')).toBe(true);
    expect(form.controls.password.hasError('required')).toBe(true);
    expect(form.controls.confirmPassword.hasError('required')).toBe(true);
  });

  it('should show passwordMismatch error when passwords do not match', () => {
    const form = component['form'];
    form.controls.password.setValue('Password1');
    form.controls.confirmPassword.setValue('Different1');
    expect(form.hasError('passwordMismatch')).toBe(true);
  });

  it('should not show passwordMismatch error when passwords match', () => {
    const form = component['form'];
    form.controls.password.setValue('Password1');
    form.controls.confirmPassword.setValue('Password1');
    expect(form.hasError('passwordMismatch')).toBe(false);
  });

  it('should validate email format', () => {
    component['form'].controls.email.setValue('not-an-email');
    expect(component['form'].controls.email.hasError('email')).toBe(true);
  });

  it('should validate password min length', () => {
    component['form'].controls.password.setValue('short');
    expect(component['form'].controls.password.hasError('minlength')).toBe(true);
  });

  it('should call AuthService.register on valid submit', () => {
    const mockResponse = {
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2026-12-31',
      user: { id: '1', email: 'test@example.com', firstName: 'John', lastName: 'Doe' },
    };
    authService.register.mockReturnValue(of(mockResponse));

    component['form'].setValue({
      firstName: 'John',
      lastName: 'Doe',
      email: 'test@example.com',
      password: 'Password1',
      confirmPassword: 'Password1',
    });
    component.onSubmit();

    expect(authService.register).toHaveBeenCalled();
  });

  it('should set error on registration failure', () => {
    authService.register.mockReturnValue(
      throwError(() => ({ error: { errors: ['Email already exists'] } })),
    );

    component['form'].setValue({
      firstName: 'John',
      lastName: 'Doe',
      email: 'test@example.com',
      password: 'Password1',
      confirmPassword: 'Password1',
    });
    component.onSubmit();

    expect(component['error']()).toBe('Email already exists');
    expect(component['loading']()).toBe(false);
  });
});
