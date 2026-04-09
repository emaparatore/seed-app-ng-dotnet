import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatDialogRef } from '@angular/material/dialog';
import { ConsentUpdateDialog } from './consent-update-dialog';

describe('ConsentUpdateDialog', () => {
  let component: ConsentUpdateDialog;
  let fixture: ComponentFixture<ConsentUpdateDialog>;
  let dialogRef: { close: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    dialogRef = { close: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [ConsentUpdateDialog],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: MatDialogRef, useValue: dialogRef },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ConsentUpdateDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should close dialog with true on accept', () => {
    component.onAccept();
    expect(dialogRef.close).toHaveBeenCalledWith(true);
  });

  it('should close dialog with false on decline', () => {
    component.onDecline();
    expect(dialogRef.close).toHaveBeenCalledWith(false);
  });
});
