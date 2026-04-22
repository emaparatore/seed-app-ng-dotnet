import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { InvoiceRequests } from './invoice-requests';
import { BillingService } from '../../pricing/billing.service';
import { InvoiceRequest } from '../../pricing/billing.models';
import { InvoiceRequestDetailDialog } from '../../shared/invoice-request-detail-dialog/invoice-request-detail-dialog';

describe('InvoiceRequests', () => {
  let component: InvoiceRequests;
  let fixture: ComponentFixture<InvoiceRequests>;
  let billingService: {
    getMyInvoiceRequests: ReturnType<typeof vi.fn>;
    createInvoiceRequest: ReturnType<typeof vi.fn>;
  };
  let dialog: { open: ReturnType<typeof vi.fn> };

  const mockRequest: InvoiceRequest = {
    id: 'req-1',
    customerType: 'Company',
    fullName: 'Mario Rossi',
    companyName: 'Rossi SRL',
    address: 'Via Roma 1',
    city: 'Roma',
    postalCode: '00100',
    country: 'Italia',
    fiscalCode: null,
    vatNumber: 'IT12345678901',
    sdiCode: 'ABC1234',
    pecEmail: 'amministrazione@rossipec.it',
    stripePaymentIntentId: null,
    status: 'Requested',
    createdAt: '2026-04-22T08:30:00Z',
    processedAt: null,
  };

  beforeEach(async () => {
    billingService = {
      getMyInvoiceRequests: vi.fn().mockReturnValue(of([mockRequest])),
      createInvoiceRequest: vi.fn().mockReturnValue(of('req-2')),
    };

    dialog = {
      open: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [InvoiceRequests],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: BillingService, useValue: billingService },
        { provide: MatDialog, useValue: dialog },
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(InvoiceRequests);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load invoice requests on init', () => {
    fixture.detectChanges();

    expect(billingService.getMyInvoiceRequests).toHaveBeenCalled();
    expect(component['requests']()).toEqual([mockRequest]);
  });

  it('should open detail dialog with selected request', () => {
    component['openDetails'](mockRequest);

    expect(dialog.open).toHaveBeenCalledWith(InvoiceRequestDetailDialog, {
      width: '720px',
      maxWidth: '96vw',
      data: mockRequest,
    });
  });
});
