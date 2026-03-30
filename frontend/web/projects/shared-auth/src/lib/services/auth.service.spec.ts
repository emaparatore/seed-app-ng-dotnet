import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { AUTH_CONFIG } from '../auth.config';
import { AuthResponse } from '../models/auth.models';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let router: { navigate: ReturnType<typeof vi.fn>; createUrlTree: ReturnType<typeof vi.fn> };

  const mockAuthResponse: AuthResponse = {
    accessToken: 'test-access-token',
    refreshToken: 'test-refresh-token',
    expiresAt: '2026-12-31T00:00:00Z',
    user: { id: '1', email: 'test@example.com', firstName: 'John', lastName: 'Doe', roles: [], permissions: [] },
    permissions: [],
    mustChangePassword: false,
  };

  beforeEach(() => {
    localStorage.clear();
    router = { navigate: vi.fn(), createUrlTree: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AUTH_CONFIG, useValue: { apiUrl: 'http://localhost:5000/api/v1.0' } },
        { provide: Router, useValue: router },
      ],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);

    // Drain any initial getProfile request from loadFromStorage
    httpMock.match(() => true);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should not be authenticated initially', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
  });

  describe('login', () => {
    it('should store tokens and user on success', () => {
      service.login({ email: 'test@example.com', password: 'Password1' }).subscribe((res) => {
        expect(res.accessToken).toBe('test-access-token');
        expect(service.accessToken()).toBe('test-access-token');
        expect(service.currentUser()?.email).toBe('test@example.com');
        expect(localStorage.getItem('accessToken')).toBe('test-access-token');
        expect(localStorage.getItem('refreshToken')).toBe('test-refresh-token');
      });

      const req = httpMock.expectOne('http://localhost:5000/api/v1.0/auth/login');
      expect(req.request.method).toBe('POST');
      req.flush(mockAuthResponse);
    });
  });

  describe('register', () => {
    it('should return message and not store tokens', () => {
      service
        .register({
          email: 'test@example.com',
          password: 'Password1',
          firstName: 'John',
          lastName: 'Doe',
        })
        .subscribe((res) => {
          expect(res.message).toBe('Please check your email to verify your account.');
          expect(service.currentUser()).toBeNull();
          expect(localStorage.getItem('accessToken')).toBeNull();
        });

      const req = httpMock.expectOne('http://localhost:5000/api/v1.0/auth/register');
      expect(req.request.method).toBe('POST');
      req.flush({ message: 'Please check your email to verify your account.' });
    });
  });

  describe('confirmEmail', () => {
    it('should store tokens and user on success', () => {
      service.confirmEmail('test@example.com', 'valid-token').subscribe((res) => {
        expect(res.accessToken).toBe('test-access-token');
        expect(service.currentUser()?.email).toBe('test@example.com');
        expect(localStorage.getItem('accessToken')).toBe('test-access-token');
      });

      const req = httpMock.expectOne('http://localhost:5000/api/v1.0/auth/confirm-email');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ email: 'test@example.com', token: 'valid-token' });
      req.flush(mockAuthResponse);
    });
  });

  describe('logout', () => {
    it('should clear auth state and navigate to login', () => {
      // First login
      service.login({ email: 'test@example.com', password: 'Password1' }).subscribe();
      httpMock.expectOne('http://localhost:5000/api/v1.0/auth/login').flush(mockAuthResponse);

      service.logout();

      // Drain the logout POST (best effort)
      httpMock.match('http://localhost:5000/api/v1.0/auth/logout');

      expect(service.currentUser()).toBeNull();
      expect(service.accessToken()).toBeNull();
      expect(localStorage.getItem('accessToken')).toBeNull();
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });
  });

  describe('getProfile', () => {
    it('should update currentUser and permissions signals', () => {
      const user = { id: '1', email: 'me@example.com', firstName: 'Jane', lastName: 'Doe', roles: [], permissions: ['users.read'] };

      service.getProfile().subscribe((res) => {
        expect(res.email).toBe('me@example.com');
        expect(service.currentUser()?.email).toBe('me@example.com');
        expect(service.permissions()).toContain('users.read');
      });

      const req = httpMock.expectOne('http://localhost:5000/api/v1.0/auth/me');
      expect(req.request.method).toBe('GET');
      req.flush(user);
    });
  });

  describe('refreshToken', () => {
    it('should update tokens on success', () => {
      localStorage.setItem('refreshToken', 'old-refresh');

      service.refreshToken().subscribe((res) => {
        expect(res.accessToken).toBe('test-access-token');
        expect(service.accessToken()).toBe('test-access-token');
      });

      const req = httpMock.expectOne('http://localhost:5000/api/v1.0/auth/refresh');
      expect(req.request.method).toBe('POST');
      expect(req.request.body.refreshToken).toBe('old-refresh');
      req.flush(mockAuthResponse);
    });

    it('should clear auth on failure', () => {
      localStorage.setItem('refreshToken', 'old-refresh');

      service.refreshToken().subscribe({
        error: () => {
          expect(service.currentUser()).toBeNull();
          expect(service.accessToken()).toBeNull();
        },
      });

      httpMock
        .expectOne('http://localhost:5000/api/v1.0/auth/refresh')
        .flush('error', { status: 401, statusText: 'Unauthorized' });
    });
  });
});
