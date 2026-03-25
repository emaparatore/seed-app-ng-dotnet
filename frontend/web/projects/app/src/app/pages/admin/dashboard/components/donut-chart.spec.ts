import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ComponentRef } from '@angular/core';
import { DonutChart } from './donut-chart';
import { RoleDistribution } from '../models/dashboard.models';

describe('DonutChart', () => {
  let fixture: ComponentFixture<DonutChart>;
  let componentRef: ComponentRef<DonutChart>;

  const testData: RoleDistribution[] = [
    { roleName: 'Admin', userCount: 5 },
    { roleName: 'User', userCount: 95 },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DonutChart],
    }).compileComponents();

    fixture = TestBed.createComponent(DonutChart);
    componentRef = fixture.componentRef;
  });

  it('should create', () => {
    componentRef.setInput('data', []);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render legend with role names', () => {
    componentRef.setInput('data', testData);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const labels = el.querySelectorAll('.legend-label');
    expect(labels.length).toBe(2);
    expect(labels[0].textContent?.trim()).toBe('Admin');
    expect(labels[1].textContent?.trim()).toBe('User');
  });
});
