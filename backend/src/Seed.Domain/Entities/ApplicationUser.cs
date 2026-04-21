using Microsoft.AspNetCore.Identity;

namespace Seed.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public DateTime? PrivacyPolicyAcceptedAt { get; set; }
    public DateTime? TermsAcceptedAt { get; set; }
    public string? ConsentVersion { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<UserSubscription> Subscriptions { get; set; } = [];
    public ICollection<InvoiceRequest> InvoiceRequests { get; set; } = [];
}
