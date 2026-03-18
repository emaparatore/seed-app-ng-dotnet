using System.Text.RegularExpressions;
using FluentAssertions;
using Seed.Domain.Authorization;

namespace Seed.UnitTests.Domain;

public class PermissionsTests
{
    [Fact]
    public void GetAll_Should_Return_16_Permissions()
    {
        Permissions.GetAll().Should().HaveCount(16);
    }

    [Fact]
    public void GetAll_Should_Return_Unique_Permissions()
    {
        var all = Permissions.GetAll();
        all.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_Permissions_Should_Have_Category_Dot_Action_Format()
    {
        var regex = new Regex(@"^\w+\.\w+$");

        foreach (var permission in Permissions.GetAll())
        {
            regex.IsMatch(permission).Should().BeTrue(
                because: $"permission '{permission}' should match 'Category.Action' format");
        }
    }

    [Fact]
    public void All_Expected_Categories_Should_Be_Present()
    {
        var categories = Permissions.GetAll()
            .Select(p => p.Split('.')[0])
            .Distinct()
            .ToList();

        categories.Should().Contain("Users");
        categories.Should().Contain("Roles");
        categories.Should().Contain("AuditLog");
        categories.Should().Contain("Settings");
        categories.Should().Contain("Dashboard");
        categories.Should().Contain("SystemHealth");
        categories.Should().HaveCount(6);
    }

    [Fact]
    public void Permission_Constants_Should_Match_GetAll()
    {
        var all = Permissions.GetAll();

        all.Should().Contain(Permissions.Users.Read);
        all.Should().Contain(Permissions.Users.Create);
        all.Should().Contain(Permissions.Users.Update);
        all.Should().Contain(Permissions.Users.Delete);
        all.Should().Contain(Permissions.Users.ToggleStatus);
        all.Should().Contain(Permissions.Users.AssignRoles);
        all.Should().Contain(Permissions.Roles.Read);
        all.Should().Contain(Permissions.Roles.Create);
        all.Should().Contain(Permissions.Roles.Update);
        all.Should().Contain(Permissions.Roles.Delete);
        all.Should().Contain(Permissions.AuditLog.Read);
        all.Should().Contain(Permissions.AuditLog.Export);
        all.Should().Contain(Permissions.Settings.Read);
        all.Should().Contain(Permissions.Settings.Manage);
        all.Should().Contain(Permissions.Dashboard.ViewStats);
        all.Should().Contain(Permissions.SystemHealth.Read);
    }
}
