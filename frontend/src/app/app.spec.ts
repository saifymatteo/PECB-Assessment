import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideNoopAnimations()],
    }).compileComponents();
  });

  it('shows the Support Desk brand', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const brand = fixture.nativeElement.querySelector('.brand');
    expect(brand?.textContent).toContain('Support Desk');
  });
});
