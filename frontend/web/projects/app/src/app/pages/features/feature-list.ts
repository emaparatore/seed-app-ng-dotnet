import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { seedFeatures } from './feature-catalog.data';

@Component({
  selector: 'app-feature-list',
  imports: [RouterLink],
  templateUrl: './feature-list.html',
  styleUrl: './feature-list.scss',
})
export class FeatureList {
  protected readonly seedFeatures = seedFeatures;
}
