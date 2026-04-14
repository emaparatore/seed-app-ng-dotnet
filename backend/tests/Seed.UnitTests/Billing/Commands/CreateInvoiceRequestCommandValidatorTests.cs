using FluentAssertions;
using Seed.Application.Billing.Commands.CreateInvoiceRequest;
using Seed.Domain.Enums;

namespace Seed.UnitTests.Billing.Commands;

public class CreateInvoiceRequestCommandValidatorTests
{
    private readonly CreateInvoiceRequestCommandValidator _validator = new();

    private static CreateInvoiceRequestCommand ValidIndividualCommand() =>
        new(
            CustomerType: CustomerType.Individual,
            FullName: "Mario Rossi",
            CompanyName: null,
            Address: "Via Roma 1",
            City: "Milano",
            PostalCode: "20100",
            Country: "IT",
            FiscalCode: "RSSMRA80A01H501Z",
            VatNumber: null,
            SdiCode: null,
            PecEmail: null,
            StripePaymentIntentId: null);

    private static CreateInvoiceRequestCommand ValidCompanyCommand() =>
        new(
            CustomerType: CustomerType.Company,
            FullName: "Mario Rossi",
            CompanyName: "ACME Srl",
            Address: "Via Roma 1",
            City: "Milano",
            PostalCode: "20100",
            Country: "IT",
            FiscalCode: null,
            VatNumber: "IT12345678901",
            SdiCode: null,
            PecEmail: null,
            StripePaymentIntentId: null);

    [Fact]
    public async Task Should_Pass_For_Valid_Individual_Command()
    {
        var result = await _validator.ValidateAsync(ValidIndividualCommand());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Pass_For_Valid_Company_Command()
    {
        var result = await _validator.ValidateAsync(ValidCompanyCommand());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Fail_When_FullName_Is_Empty(string? fullName)
    {
        var command = ValidIndividualCommand() with { FullName = fullName! };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FullName");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Fail_When_Address_Is_Empty(string? address)
    {
        var command = ValidIndividualCommand() with { Address = address! };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Address");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Fail_When_City_Is_Empty(string? city)
    {
        var command = ValidIndividualCommand() with { City = city! };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "City");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Fail_When_PostalCode_Is_Empty(string? postalCode)
    {
        var command = ValidIndividualCommand() with { PostalCode = postalCode! };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PostalCode");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Fail_When_Country_Is_Empty(string? country)
    {
        var command = ValidIndividualCommand() with { Country = country! };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Country");
    }

    [Fact]
    public async Task Should_Fail_When_Company_Type_Without_CompanyName()
    {
        var command = ValidCompanyCommand() with { CompanyName = null };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CompanyName");
    }

    [Fact]
    public async Task Should_Fail_When_Company_Type_Without_VatNumber()
    {
        var command = ValidCompanyCommand() with { VatNumber = null };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "VatNumber");
    }

    [Fact]
    public async Task Should_Pass_When_Individual_Type_Without_CompanyName_And_VatNumber()
    {
        var command = ValidIndividualCommand() with { CompanyName = null, VatNumber = null };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }
}
