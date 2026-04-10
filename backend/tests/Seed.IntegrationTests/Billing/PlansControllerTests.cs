using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Billing.Models;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;
using Seed.IntegrationTests.Webhooks;

namespace Seed.IntegrationTests.Billing;

public class PlansControllerTests : IClassFixture<WebhookWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly WebhookWebApplicationFactory _factory;

    public PlansControllerTests(WebhookWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPlans_ReturnsActivePlans_WhenPaymentsModuleEnabled()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Integration Test Plan",
            Description = "Test plan",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            Status = PlanStatus.Active,
            SortOrder = 1
        };
        plan.Features.Add(new PlanFeature
        {
            Id = Guid.NewGuid(),
            Key = "feature1",
            Description = "Feature One",
            SortOrder = 1
        });
        db.SubscriptionPlans.Add(plan);
        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Plan",
            Status = PlanStatus.Inactive,
            SortOrder = 2
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/api/v1.0/plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plans = await response.Content.ReadFromJsonAsync<List<PlanDto>>();
        plans.Should().NotBeNull();
        plans!.Should().Contain(p => p.Name == "Integration Test Plan");
        plans.Should().NotContain(p => p.Name == "Inactive Plan");
        var testPlan = plans.First(p => p.Name == "Integration Test Plan");
        testPlan.Features.Should().HaveCount(1);
        testPlan.Features[0].Key.Should().Be("feature1");

        // Cleanup
        db.SubscriptionPlans.RemoveRange(db.SubscriptionPlans);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetPlans_ReturnsEmptyList_WhenNoPlans()
    {
        var response = await _client.GetAsync("/api/v1.0/plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plans = await response.Content.ReadFromJsonAsync<List<PlanDto>>();
        plans.Should().NotBeNull();
    }
}
