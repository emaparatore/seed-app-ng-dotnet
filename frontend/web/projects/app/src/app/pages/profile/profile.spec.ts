import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { Profile } from './profile';
import { AuthService } from 'shared-auth';

describe('Profile', () => {
  let component: Profile;
  let fixture: ComponentFixture<Profile>;
  let authService: {
    getProfile: ReturnType<typeof vi.fn>;
    deleteAccount: ReturnType<typeof vi.fn>;
    currentUser: any;
    isAuthenticated: any;
    accessToken: any;
  };
  let dialog: { open: ReturnType<typeof vi.fn> };
  const mockUser = { id: '1', email: 'test@example.com', firstName: 'John', lastName: 'Doe' };

  beforeEach(async () => {
    authService = {
      getProfile: vi.fn().mockReturnValue(of(mockUser)),
      deleteAccount: vi.fn().mockReturnValue(of(void 0)),
      currentUser: signal(mockUser),
      isAuthenticated: signal(true),
      accessToken: signal('test-token'),
    };

    dialog = {
      open: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AuthService, useValue: authService },
        { provide: MatDialog, useValue: dialog },
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

  it('should open delete dialog and call deleteAccount on confirm', () => {
    const dialogRef = { afterClosed: () => of('myPassword') } as unknown as MatDialogRef<any>;
    dialog.open.mockReturnValue(dialogRef);

    component.deleteAccount();

    expect(dialog.open).toHaveBeenCalled();
    expect(authService.deleteAccount).toHaveBeenCalledWith('myPassword');
  });

  it('should not call deleteAccount when dialog is cancelled', () => {
    const dialogRef = { afterClosed: () => of(undefined) } as unknown as MatDialogRef<any>;
    dialog.open.mockReturnValue(dialogRef);

    component.deleteAccount();

    expect(dialog.open).toHaveBeenCalled();
    expect(authService.deleteAccount).not.toHaveBeenCalled();
  });

  it('should set error when deleteAccount fails', () => {
    const dialogRef = { afterClosed: () => of('myPassword') } as unknown as MatDialogRef<any>;
    dialog.open.mockReturnValue(dialogRef);
    authService.deleteAccount.mockReturnValue(
      throwError(() => ({ error: { errors: ['Invalid password.'] } })),
    );

    component.deleteAccount();

    expect(component['error']()).toBe('Invalid password.');
    expect(component['deleting']()).toBe(false);
  });
});
