import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError, shareReplay, finalize, firstValueFrom } from 'rxjs';
import {
  AuthResponse,
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
  private _refreshInProgress: Observable<AuthResponse> | null = null;

  readonly currentUser = this._currentUser.asReadonly();
  readonly accessToken = this._accessToken.asReadonly();
  readonly isAuthenticated = computed(() => this._currentUser() !== null);

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
    if (typeof window !== 'undefined') {
      localStorage.setItem('accessToken', response.accessToken);
      localStorage.setItem('refreshToken', response.refreshToken);
    }
  }

  private clearAuth(): void {
    this._accessToken.set(null);
    this._currentUser.set(null);
    if (typeof window !== 'undefined') {
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
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
    return firstValueFrom(this.getProfile()).then(
      () => {},
      () => this.clearAuth(),
    );
  }
}
