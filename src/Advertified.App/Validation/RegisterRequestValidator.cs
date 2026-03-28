using Advertified.App.Contracts.Auth;
using FluentValidation;

namespace Advertified.App.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private static readonly HashSet<string> AllowedRevenueBands = new(StringComparer.OrdinalIgnoreCase)
    {
        "under_r1m",
        "r1m_r5m",
        "r5m_r20m",
        "r20m_r100m",
        "over_r100m"
    };

    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .Must(RegistrationValidators.HasNameAndSurname)
            .WithMessage("Enter your name and surname.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Phone)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty()
            .Must(RegistrationValidators.IsStrongPassword)
            .WithMessage("Password must be at least 12 characters and include uppercase, lowercase, a number, and a special character.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match.");

        RuleFor(x => x.BusinessName).NotEmpty();
        RuleFor(x => x.BusinessType).NotEmpty();

        RuleFor(x => x.RegistrationNumber)
            .NotEmpty()
            .Must(RegistrationValidators.IsSaCompanyRegistrationNumber)
            .WithMessage("Enter a valid South African company registration number, for example 2024/123456/07.");

        RuleFor(x => x.Industry).NotEmpty();

        RuleFor(x => x.AnnualRevenueBand)
            .NotEmpty()
            .Must(v => AllowedRevenueBands.Contains(v))
            .WithMessage("Select a valid annual revenue band.");

        RuleFor(x => x.StreetAddress).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.Province).NotEmpty();

        When(x => x.IsSouthAfricanCitizen, () =>
        {
            RuleFor(x => x.SaIdNumber)
                .NotEmpty()
                .Must(v => v != null && RegistrationValidators.IsSaIdNumber(v))
                .WithMessage("Enter a valid 13-digit South African ID number.");

            RuleFor(x => x.PassportNumber).Empty();
            RuleFor(x => x.PassportCountryIso2).Empty();
            RuleFor(x => x.PassportIssueDate).Null();
            RuleFor(x => x.PassportValidUntil).Null();
        });

        When(x => !x.IsSouthAfricanCitizen, () =>
        {
            RuleFor(x => x.PassportNumber)
                .NotEmpty();

            RuleFor(x => x.PassportCountryIso2)
                .NotEmpty()
                .Must(v => v != null && RegistrationValidators.IsIso2CountryCode(v))
                .WithMessage("Enter a valid ISO-2 country code.");

            RuleFor(x => x.PassportIssueDate)
                .NotNull();

            RuleFor(x => x.PassportValidUntil)
                .NotNull()
                .Must((x, validUntil) => x.PassportIssueDate.HasValue && validUntil.HasValue && validUntil.Value > x.PassportIssueDate.Value)
                .WithMessage("Passport valid until date must be after passport issue date.")
                .Must(validUntil => validUntil.HasValue && validUntil.Value > DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Passport must still be valid.");
        });
    }
}
