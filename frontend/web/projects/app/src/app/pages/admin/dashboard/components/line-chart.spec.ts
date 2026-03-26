import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ComponentRef } from '@angular/core';
import { LineChart } from './line-chart';
import { DailyRegistration } from '../models/dashboard.models';

describe('LineChart', () => {
  let fixture: ComponentFixture<LineChart>;
  let componentRef: ComponentRef<LineChart>;

  const testData: DailyRegistration[] = [
    { date: '2026-03-21', count: 2 },
    { date: '2026-03-22', count: 5 },
    { date: '2026-03-23', count: 3 },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LineChart],
    }).compileComponents();

    fixture = TestBed.createComponent(LineChart);
    componentRef = fixture.componentRef;
  });

  it('should create', () => {
    componentRef.setInput('data', []);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render SVG with data points', () => {
    componentRef.setInput('data', testData);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const svg = el.querySelector('svg');
    expect(svg).toBeTruthy();

    const circles = el.querySelectorAll('circle');
    expect(circles.length).toBe(testData.length);
  });
});
