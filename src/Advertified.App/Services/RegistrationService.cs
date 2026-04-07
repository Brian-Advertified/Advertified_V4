using Advertified.App.Contracts.Auth;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advertified.App.Services;

public sealed class RegistrationService : IRegistrationService
{
    private readonly AppDbContext _db;
    private readonly RegisterRequestValidator _validator;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IProspectLeadLinkingService _prospectLeadLinkingService;

    public RegistrationService(
        AppDbContext db,
        RegisterRequestValidator validator,
        IEmailVerificationService emailVerificationService,
        IPasswordHashingService passwordHashingService,
        IProspectLeadLinkingService prospectLeadLinkingService)
    {
        _db = db;
        _validator = validator;
        _emailVerificationService = emailVerificationService;
        _passwordHashingService = passwordHashingService;
        _prospectLeadLinkingService = prospectLeadLinkingService;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _db.UserAccounts
            .Include(x => x.BusinessProfile)
            .Include(x => x.IdentityProfile)
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        var now = DateTime.UtcNow;
        var user = existingUser;
        if (user is null)
        {
            user = new UserAccount
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
                UpdatedAt = now
            };

            _db.UserAccounts.Add(user);
        }
        else if (!CanCompletePendingClientRegistration(user))
        {
            throw new InvalidOperationException("A user with this email address already exists.");
        }

        user.Role = UserRole.Client;
        user.AccountStatus = AccountStatus.PendingVerification;
        user.FullName = request.FullName.Trim();
        user.Email = normalizedEmail;
        user.Phone = request.Phone.Trim();
        user.IsSaCitizen = request.IsSouthAfricanCitizen;
        user.EmailVerified = false;
        user.PhoneVerified = false;
        user.UpdatedAt = now;

        if (user.BusinessProfile is null)
        {
            user.BusinessProfile = new BusinessProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = now
            };
            _db.BusinessProfiles.Add(user.BusinessProfile);
        }
        user.BusinessProfile.BusinessName = request.BusinessName.Trim();
        user.BusinessProfile.BusinessType = request.BusinessType.Trim();
        user.BusinessProfile.RegistrationNumber = request.RegistrationNumber.Trim();
        user.BusinessProfile.VatNumber = request.VatNumber?.Trim();
        user.BusinessProfile.Industry = request.Industry.Trim();
        user.BusinessProfile.AnnualRevenueBand = request.AnnualRevenueBand.Trim();
        user.BusinessProfile.TradingAsName = request.TradingAsName?.Trim();
        user.BusinessProfile.StreetAddress = request.StreetAddress.Trim();
        user.BusinessProfile.City = request.City.Trim();
        user.BusinessProfile.Province = request.Province.Trim();
        user.BusinessProfile.VerificationStatus = VerificationStatus.NotSubmitted;
        user.BusinessProfile.UpdatedAt = now;

        if (user.IdentityProfile is null)
        {
            user.IdentityProfile = new IdentityProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = now
            };
            _db.IdentityProfiles.Add(user.IdentityProfile);
        }
        user.IdentityProfile.SaIdNumber = request.IsSouthAfricanCitizen ? request.SaIdNumber?.Trim() : null;
        user.IdentityProfile.PassportNumber = request.IsSouthAfricanCitizen ? null : request.PassportNumber?.Trim();
        user.IdentityProfile.PassportCountryIso2 = request.IsSouthAfricanCitizen ? null : request.PassportCountryIso2?.Trim().ToUpperInvariant();
        user.IdentityProfile.PassportIssueDate = request.IsSouthAfricanCitizen ? null : request.PassportIssueDate;
        user.IdentityProfile.PassportValidUntil = request.IsSouthAfricanCitizen ? null : request.PassportValidUntil;
        user.IdentityProfile.IdentityType = request.IsSouthAfricanCitizen ? IdentityType.SaId : IdentityType.Passport;
        user.IdentityProfile.VerificationStatus = VerificationStatus.NotSubmitted;
        user.IdentityProfile.UpdatedAt = now;

        user.PasswordHash = _passwordHashingService.HashPassword(user, request.Password);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (TryMapRegistrationConflict(ex, out var message))
        {
            throw new InvalidOperationException(message, ex);
        }

        await _prospectLeadLinkingService.ClaimByEmailAsync(user, cancellationToken);
        await _emailVerificationService.QueueActivationEmailAsync(user, request.NextPath, cancellationToken);

        return new RegisterResponse
        {
            UserId = user.Id,
            Email = user.Email,
            EmailVerificationRequired = true,
            AccountStatus = "pending_verification"
        };
    }

    private static bool CanCompletePendingClientRegistration(UserAccount user)
    {
        return user.Role == UserRole.Client
            && user.AccountStatus == AccountStatus.PendingVerification
            && !user.EmailVerified;
    }

    private static bool TryMapRegistrationConflict(DbUpdateException exception, out string message)
    {
        if (exception.InnerException is not PostgresException postgres
            || postgres.SqlState != PostgresErrorCodes.UniqueViolation)
        {
            message = string.Empty;
            return false;
        }

        message = postgres.ConstraintName switch
        {
            "uq_user_accounts_email" => "A user with this email address already exists.",
            "uq_business_profiles_registration_number" => "A business with this registration number already exists.",
            "uq_identity_profiles_sa_id_number" => "An account with this South African ID number already exists.",
            "uq_identity_profiles_passport" => "An account with this passport number already exists.",
            _ => "This account information already exists."
        };

        return true;
    }
}
