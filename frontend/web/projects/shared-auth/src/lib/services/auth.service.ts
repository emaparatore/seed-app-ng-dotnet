import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError, shareReplay, finalize, firstValueFrom } from 'rxjs';
import {
  AuthResponse,
  ChangePasswordRequest,
  ForgotPasswordRequest,
  LoginRequest,
  MessageResponse,
  RegisterRequest,
  ResetPasswordRequest,
  User,
} from '../models/auth.models';
import { AUTH_CONFIG } from '../auth.config';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly apiUrl = `${inject(AUTH_CONFIG).apiUrl}/auth`;

  private readonly _currentUser = signal<User | null>(null);
  private readonly _accessToken = signal<string | null>(null);
  private readonly _mustChangePassword = signal(false);
  private readonly _permissions = signal<string[]>([]);
  private _refreshInProgress: Observable<AuthResponse> | null = null;

  readonly currentUser = this._currentUser.asReadonly();
  readonly accessToken = this._accessToken.asReadonly();
  readonly isAuthenticated = computed(() => this._currentUser() !== null);
  readonly mustChangePassword = this._mustChangePassword.asReadonly();
  readonly permissions = this._permissions.asReadonly();

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, request).pipe(
      tap((response) => this.handleAuthResponse(response)),
    );
  }

  register(request: RegisterRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.apiUrl}/register`, request);
  }

  confirmEmail(email: string, token: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/confirm-email`, { email, token }).pipe(
      tap((response) => this.handleAuthResponse(response)),
    );
  }

  refreshToken(): Observable<AuthResponse> {
    if (this._refreshInProgress) {
      return this._refreshInProgress;
    }

    const refreshToken = this.getRefreshToken();
    this._refreshInProgress = this.http
      .post<AuthResponse>(`${this.apiUrl}/refresh`, { refreshToken })
      .pipe(
        tap((response) => this.handleAuthResponse(response)),
        catchError((error) => {
          this.clearAuth();
          return throwError(() => error);
        }),
        shareReplay(1),
        finalize(() => (this._refreshInProgress = null)),
      );

    return this._refreshInProgress;
  }

  logout(): void {
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.http.post(`${this.apiUrl}/logout`, { refreshToken }).subscribe();
    }
    this.clearAuth();
    this.router.navigate(['/login']);
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.apiUrl}/forgot-password`, request);
  }

  resetPassword(request: ResetPasswordRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.apiUrl}/reset-password`, request);
  }

  changePassword(request: ChangePasswordRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.apiUrl}/change-password`, request).pipe(
      tap(() => {
        this._mustChangePassword.set(false);
        if (typeof window !== 'undefined') {
          localStorage.removeItem('mustChangePassword');
        }
      }),
    );
  }

  setMustChangePassword(value: boolean): void {
    this._mustChangePassword.set(value);
    if (typeof window !== 'undefined') {
      if (value) {
        localStorage.setItem('mustChangePassword', 'true');
      } else {
        localStorage.removeItem('mustChangePassword');
      }
    }
  }

  deleteAccount(password: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/account`, { body: { password } }).pipe(
      tap(() => {
        this.clearAuth();
        this.router.navigate(['/login']);
      }),
    );
  }

  getProfile(): Observable<User> {
    return this.http.get<User>(`${this.apiUrl}/me`).pipe(tap((user) => this._currentUser.set(user)));
  }

  private handleAuthResponse(response: AuthResponse): void {
    this._accessToken.set(response.accessToken);
    this._currentUser.set(response.user);
    this._mustChangePassword.set(response.mustChangePassword);
    this._permissions.set(response.permissions ?? []);
    if (typeof window !== 'undefined') {
      localStorage.setItem('accessToken', response.accessToken);
      localStorage.setItem('refreshToken', response.refreshToken);
      if (response.mustChangePassword) {
        localStorage.setItem('mustChangePassword', 'true');
      } else {
        localStorage.removeItem('mustChangePassword');
      }
    }
  }

  private clearAuth(): void {
    this._accessToken.set(null);
    this._currentUser.set(null);
    this._mustChangePassword.set(false);
    this._permissions.set([]);
    if (typeof window !== 'undefined') {
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
      localStorage.removeItem('mustChangePassword');
    }
  }

  private getRefreshToken(): string | null {
    if (typeof window === 'undefined') return null;
    return localStorage.getItem('refreshToken');
  }

  initializeAuth(): Promise<void> {
    if (typeof window === 'undefined') return Promise.resolve();
    const token = localStorage.getItem('accessToken');
    if (!token) return Promise.resolve();

    this._accessToken.set(token);
    const mustChange = localStorage.getItem('mustChangePassword') === 'true';
    this._mustChangePassword.set(mustChange);

    return firstValueFrom(this.getProfile()).then(
      () => {},
      () => this.clearAuth(),
    );
  }
}
