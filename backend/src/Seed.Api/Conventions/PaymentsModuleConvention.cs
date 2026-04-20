using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Seed.Api.Controllers;

namespace Seed.Api.Conventions;

public class PaymentsModuleConvention : IApplicationModelConvention
{
    private static readonly HashSet<Type> BillingControllerTypes =
    [
        typeof(BillingController),
        typeof(StripeWebhookController),
        typeof(AdminPlansController),
        typeof(AdminSubscriptionsController),
        typeof(AdminInvoiceRequestsController),
        typeof(PlansController),
    ];

    public void Apply(ApplicationModel application)
    {
        var toRemove = application.Controllers
            .Where(c => BillingControllerTypes.Contains(c.ControllerType.AsType()))
            .ToList();

        foreach (var controller in toRemove)
        {
            application.Controllers.Remove(controller);
        }
    }
}
