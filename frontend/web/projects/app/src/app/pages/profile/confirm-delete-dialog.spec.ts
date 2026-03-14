import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatDialogRef } from '@angular/material/dialog';
import { ConfirmDeleteDialog } from './confirm-delete-dialog';

describe('ConfirmDeleteDialog', () => {
  let component: ConfirmDeleteDialog;
  let fixture: ComponentFixture<ConfirmDeleteDialog>;
  let dialogRef: { close: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    dialogRef = { close: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [ConfirmDeleteDialog],
      providers: [
        provideNoopAnimations(),
        { provide: MatDialogRef, useValue: dialogRef },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ConfirmDeleteDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should close dialog with password on confirm', () => {
    component['password'].set('myPassword');
    component['confirmed'].set(true);

    component.onConfirm();

    expect(dialogRef.close).toHaveBeenCalledWith('myPassword');
  });

  it('should have delete button disabled when password is empty', () => {
    component['password'].set('');
    component['confirmed'].set(true);
    fixture.detectChanges();

    const deleteButton = fixture.nativeElement.querySelector('button[color="warn"]');
    expect(deleteButton.disabled).toBe(true);
  });

  it('should have delete button disabled when checkbox is unchecked', () => {
    component['password'].set('myPassword');
    component['confirmed'].set(false);
    fixture.detectChanges();

    const deleteButton = fixture.nativeElement.querySelector('button[color="warn"]');
    expect(deleteButton.disabled).toBe(true);
  });

  it('should have delete button enabled when password and checkbox are set', () => {
    component['password'].set('myPassword');
    component['confirmed'].set(true);
    fixture.detectChanges();

    const deleteButton = fixture.nativeElement.querySelector('button[color="warn"]');
    expect(deleteButton.disabled).toBe(false);
  });
});
