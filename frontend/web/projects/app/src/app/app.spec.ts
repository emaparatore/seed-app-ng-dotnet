import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { App } from './app';
import { AuthService, AUTH_CONFIG } from 'shared-auth';

describe('App', () => {
  beforeEach(async () => {
    const mockAuthService = {
      isAuthenticated: signal(false),
      currentUser: signal(null),
      accessToken: signal(null),
      login: vi.fn(),
      logout: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: mockAuthService },
        { provide: AUTH_CONFIG, useValue: { apiUrl: 'http://localhost:5000/api/v1.0' } },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });
});
