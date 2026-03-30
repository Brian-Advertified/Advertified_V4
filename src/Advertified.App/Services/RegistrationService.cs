using Advertified.App.Contracts.Auth;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class RegistrationService : IRegistrationService
{
    private readonly AppDbContext _db;
    private readonly RegisterRequestValidator _validator;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IPasswordHashingService _passwordHashingService;

    public RegistrationService(
        AppDbContext db,
        RegisterRequestValidator validator,
        IEmailVerificationService emailVerificationService,
        IPasswordHashingService passwordHashingService)
    {
        _db = db;
        _validator = validator;
        _emailVerificationService = emailVerificationService;
        _passwordHashingService = passwordHashingService;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.UserAccounts.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("A user with this email address already exists.");
        }

        var now = DateTime.UtcNow;
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Client,
            AccountStatus = AccountStatus.PendingVerification,
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            Phone = request.Phone.Trim(),
            IsSaCitizen = request.IsSouthAfricanCitizen,
            EmailVerified = false,
            PhoneVerified = false,
            CreatedAt = now,
            UpdatedAt = now,
            BusinessProfile = new BusinessProfile
            {
                Id = Guid.NewGuid(),
                BusinessName = request.BusinessName.Trim(),
                BusinessType = request.BusinessType.Trim(),
                RegistrationNumber = request.RegistrationNumber.Trim(),
                VatNumber = request.VatNumber?.Trim(),
                Industry = request.Industry.Trim(),
                AnnualRevenueBand = request.AnnualRevenueBand.Trim(),
                TradingAsName = request.TradingAsName?.Trim(),
                StreetAddress = request.StreetAddress.Trim(),
                City = request.City.Trim(),
                Province = request.Province.Trim(),
                VerificationStatus = VerificationStatus.NotSubmitted,
                CreatedAt = now,
                UpdatedAt = now
            },
            IdentityProfile = new IdentityProfile
            {
                Id = Guid.NewGuid(),
                SaIdNumber = request.SaIdNumber?.Trim(),
                PassportNumber = request.PassportNumber?.Trim(),
                PassportCountryIso2 = request.PassportCountryIso2?.Trim().ToUpperInvariant(),
                PassportIssueDate = request.PassportIssueDate,
                PassportValidUntil = request.PassportValidUntil,
                IdentityType = request.IsSouthAfricanCitizen ? IdentityType.SaId : IdentityType.Passport,
                VerificationStatus = VerificationStatus.NotSubmitted,
                CreatedAt = now,
                UpdatedAt = now
            }
        };
        user.PasswordHash = _passwordHashingService.HashPassword(user, request.Password);

        _db.UserAccounts.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
        await _emailVerificationService.QueueActivationEmailAsync(user, cancellationToken);

        return new RegisterResponse
        {
            UserId = user.Id,
            Email = user.Email,
            EmailVerificationRequired = true,
            AccountStatus = "pending_verification"
        };
    }
}
