import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-checkout-cancel',
  imports: [RouterLink, MatCardModule, MatButtonModule, MatIconModule],
  templateUrl: './checkout-cancel.html',
  styleUrl: './checkout-cancel.scss',
})
export class CheckoutCancel {}
