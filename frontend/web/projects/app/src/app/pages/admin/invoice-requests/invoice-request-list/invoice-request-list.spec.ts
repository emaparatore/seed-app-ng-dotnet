import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { InvoiceRequestList } from './invoice-request-list';
import { AdminInvoiceRequestsService } from '../admin-invoice-requests.service';
import { AdminInvoiceRequest } from '../admin-invoice-requests.models';
import { InvoiceRequestDetailDialog } from '../../../shared/invoice-request-detail-dialog/invoice-request-detail-dialog';

describe('InvoiceRequestList', () => {
  let component: InvoiceRequestList;
  let fixture: ComponentFixture<InvoiceRequestList>;
  let service: {
    getInvoiceRequests: ReturnType<typeof vi.fn>;
    updateInvoiceRequestStatus: ReturnType<typeof vi.fn>;
  };
  let dialog: { open: ReturnType<typeof vi.fn> };

  const mockRequest: AdminInvoiceRequest = {
    id: 'req-admin-1',
    userEmail: 'utente@example.com',
    userFullName: 'Mario Rossi',
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
    pecEmail: 'pec@rossi.it',
    userSubscriptionId: 'sub-1',
    stripeInvoiceId: 'in_1',
    currency: 'EUR',
    amountSubtotal: 19,
    amountTax: 4.18,
    amountTotal: 23.18,
    amountPaid: 23.18,
    isProrationApplied: true,
    prorationAmount: -5,
    billingReason: 'subscription_update',
    serviceName: 'Pro',
    servicePeriodStart: '2026-04-01T00:00:00Z',
    servicePeriodEnd: '2026-05-01T00:00:00Z',
    invoicePeriodStart: '2026-04-01T00:00:00Z',
    invoicePeriodEnd: '2026-05-01T00:00:00Z',
    stripePaymentIntentId: null,
    status: 'Requested',
    createdAt: '2026-04-22T09:00:00Z',
    processedAt: null,
  };

  beforeEach(async () => {
    service = {
      getInvoiceRequests: vi.fn().mockReturnValue(
        of({
          items: [mockRequest],
          pageNumber: 1,
          pageSize: 10,
          totalCount: 1,
          totalPages: 1,
          hasPreviousPage: false,
          hasNextPage: false,
        }),
      ),
      updateInvoiceRequestStatus: vi.fn().mockReturnValue(of(void 0)),
    };

    dialog = {
      open: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [InvoiceRequestList],
      providers: [
        provideNoopAnimations(),
        { provide: AdminInvoiceRequestsService, useValue: service },
        { provide: MatDialog, useValue: dialog },
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(InvoiceRequestList);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load requests on init', () => {
    fixture.detectChanges();

    expect(service.getInvoiceRequests).toHaveBeenCalled();
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
