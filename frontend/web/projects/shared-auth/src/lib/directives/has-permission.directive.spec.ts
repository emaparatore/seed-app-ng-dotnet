import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HasPermissionDirective } from './has-permission.directive';
import { PermissionService } from '../services/permission.service';

@Component({
  imports: [HasPermissionDirective],
  template: `<div *hasPermission="'Users.Read'"><span class="protected">Content</span></div>`,
})
class TestComponent {}

describe('HasPermissionDirective', () => {
  function setup(permissions: string[]) {
    const permissionsSignal = signal(permissions);
    const permissionService = {
      hasPermission: (p: string) => permissionsSignal().includes(p),
    };

    TestBed.configureTestingModule({
      imports: [TestComponent],
      providers: [{ provide: PermissionService, useValue: permissionService }],
    });

    const fixture = TestBed.createComponent(TestComponent);
    fixture.detectChanges();

    return { fixture, permissionsSignal };
  }

  it('should show content when user has permission', () => {
    const { fixture } = setup(['Users.Read']);
    const el = fixture.nativeElement.querySelector('.protected');
    expect(el).toBeTruthy();
    expect(el.textContent).toBe('Content');
  });

  it('should hide content when user lacks permission', () => {
    const { fixture } = setup([]);
    const el = fixture.nativeElement.querySelector('.protected');
    expect(el).toBeNull();
  });
});
