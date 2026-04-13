import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';

@Component({
  selector: 'app-confirm-cancel-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Cancella abbonamento</h2>
    <mat-dialog-content>
      <p>
        Sei sicuro di voler cancellare l'abbonamento? L'accesso rimarrà attivo fino alla fine del
        periodo corrente.
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Annulla</button>
      <button mat-flat-button color="warn" [mat-dialog-close]="true">
        Conferma cancellazione
      </button>
    </mat-dialog-actions>
  `,
})
export class ConfirmCancelDialog {}
