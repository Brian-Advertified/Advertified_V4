import { zodResolver } from '@hookform/resolvers/zod';
import { type AddressAutofillRetrieveResponse } from '@mapbox/search-js-core';
import { Eye, EyeOff } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useState, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { AddressAutofillInput } from './AddressAutofillInput';
import type { RegistrationSchema } from '../schemas';
import { registrationSchema } from '../schemas';
import { useSharedFormOptions } from '../../../lib/useSharedFormOptions';

export function RegistrationWizard({
  onSubmit,
  loading,
}: {
  onSubmit: (values: RegistrationSchema) => Promise<void>;
  loading: boolean;
}) {
  const mapboxAccessToken = (import.meta.env.VITE_MAPBOX_ACCESS_TOKEN as string | undefined)?.trim();
  const mapboxEnabled = Boolean(mapboxAccessToken);
  const formOptionsQuery = useSharedFormOptions();
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors },
  } = useForm<RegistrationSchema>({
    resolver: zodResolver(registrationSchema),
    mode: 'onBlur',
    shouldUnregister: true,
    defaultValues: {
      isSouthAfricanCitizen: true,
      acceptTerms: true,
      acceptPopia: true,
    } as Partial<RegistrationSchema>,
  });

  const isCitizen = watch('isSouthAfricanCitizen');

  if (formOptionsQuery.isPending) {
    return <div className="register-section">Loading form options...</div>;
  }

  if (formOptionsQuery.isError || !formOptionsQuery.data) {
    return <div className="register-section">We could not load registration options right now. Please refresh and try again.</div>;
  }

  const { businessTypes, industries, provinces, revenueBands } = formOptionsQuery.data;

  function handleAddressRetrieve(response: AddressAutofillRetrieveResponse) {
    const feature = response.features[0];
    const properties = feature?.properties;

    if (!properties) {
      return;
    }

    const streetAddress = properties.address_line1 ?? properties.full_address ?? properties.place_name ?? '';
    const city = properties.address_level2 ?? properties.address_level3 ?? '';
    const province = properties.address_level1 ?? '';

    if (streetAddress) {
      setValue('streetAddress', streetAddress, { shouldDirty: true, shouldTouch: true, shouldValidate: true });
    }

    if (city) {
      setValue('city', city, { shouldDirty: true, shouldTouch: true, shouldValidate: true });
    }

    if (province) {
      setValue('province', province, { shouldDirty: true, shouldTouch: true, shouldValidate: true });
    }
  }

  return (
    <form className="register-shell" onSubmit={handleSubmit(onSubmit)}>
      <div className="register-head">
        <p className="register-kicker">Create account</p>
        <h1 className="register-title">Register your business for Advertified</h1>
        <p className="register-copy">
          Set up your account, add the business profile, and we&apos;ll queue your activation email immediately after registration.
        </p>
      </div>

      <RegisterSection title="Account details">
        <div className="register-grid">
          <Field label="Full name *" error={errors.fullName?.message} className="register-field-full">
            <input {...register('fullName')} className="register-input" placeholder="Full name *" />
          </Field>

          <Field label="Email *" error={errors.email?.message}>
            <input {...register('email')} className="register-input" placeholder="Email *" />
          </Field>

          <Field label="Phone *" error={errors.phone?.message}>
            <input {...register('phone')} className="register-input" placeholder="Phone *" />
          </Field>

          <Field label="Citizenship *" error={errors.isSouthAfricanCitizen?.message} className="register-field-full">
            <select
              {...register('isSouthAfricanCitizen', {
                setValueAs: (value) => value === true || value === 'true',
              })}
              className="register-input"
            >
              <option value="true">South African Citizen</option>
              <option value="false">Non-South African Citizen</option>
            </select>
          </Field>

          {isCitizen ? (
            <Field label="SA ID Number *" error={errors.saIdNumber?.message} className="register-field-full">
              <input {...register('saIdNumber')} className="register-input" placeholder="SA ID Number *" />
            </Field>
          ) : (
            <>
              <Field label="Passport Number *" error={errors.passportNumber?.message}>
                <input {...register('passportNumber')} className="register-input" placeholder="Passport Number *" />
              </Field>

              <Field label="Country of issuance *" error={errors.passportCountryIso2?.message}>
                <input {...register('passportCountryIso2')} className="register-input" placeholder="Country of Issuance (ISO-2) *" />
              </Field>

              <Field label="Passport issue date *" error={errors.passportIssueDate?.message}>
                <input {...register('passportIssueDate')} type="date" className="register-input" />
              </Field>

              <Field label="Passport valid until *" error={errors.passportValidUntil?.message}>
                <input {...register('passportValidUntil')} type="date" className="register-input" />
              </Field>
            </>
          )}
        </div>
      </RegisterSection>

      <RegisterSection title="Business details">
        <div className="register-grid">
          <Field label="Business name *" error={errors.businessName?.message}>
            <input {...register('businessName')} className="register-input" placeholder="Business name *" />
          </Field>

          <Field label="Business type *" error={errors.businessType?.message}>
            <select {...register('businessType')} className="register-input">
              <option value="">Business type *</option>
              {businessTypes.map((item) => (
                <option key={item.value} value={item.value}>
                  {item.label}
                </option>
              ))}
            </select>
          </Field>

          <Field label="Registration number *" error={errors.registrationNumber?.message}>
            <input {...register('registrationNumber')} className="register-input" placeholder="Registration number *" />
          </Field>

          <Field label="Industry *" error={errors.industry?.message}>
            <select {...register('industry')} className="register-input">
              <option value="">Industry *</option>
              {industries.map((item) => (
                <option key={item.value} value={item.value}>
                  {item.label}
                </option>
              ))}
            </select>
          </Field>

          <Field label="VAT number" error={errors.vatNumber?.message}>
            <input {...register('vatNumber')} className="register-input" placeholder="VAT number" />
          </Field>

          <Field label="Annual revenue *" error={errors.annualRevenueBand?.message}>
            <select {...register('annualRevenueBand')} className="register-input">
              <option value="">Annual revenue *</option>
              {revenueBands.map((item) => (
                <option key={item.value} value={item.value}>
                  {item.label}
                </option>
              ))}
            </select>
          </Field>

          <Field label="Trading as name" error={errors.tradingAsName?.message} className="register-field-full">
            <input {...register('tradingAsName')} className="register-input" placeholder="Trading as name (optional)" />
          </Field>
        </div>
      </RegisterSection>

      <RegisterSection title="Location">
        <div className="register-grid">
          <div className="register-field register-field-full">
            <div className="rounded-[18px] border border-brand/10 bg-brand/5 px-4 py-3 text-sm leading-6 text-ink-soft">
              {mapboxEnabled
                ? 'Search for your business address and we will populate the location fields for you. You can still edit them manually if needed.'
                : 'Enter your business address manually for now. Add VITE_MAPBOX_ACCESS_TOKEN to enable address search and autofill.'}
            </div>
          </div>

          <Field label="Street address *" error={errors.streetAddress?.message} className="register-field-full">
            {mapboxEnabled ? (
              <AddressAutofillInput
                accessToken={mapboxAccessToken!}
                onRetrieve={handleAddressRetrieve}
                inputProps={{
                  ...register('streetAddress'),
                  autoComplete: 'street-address',
                  className: 'register-input',
                  placeholder: 'Search your business address *',
                }}
              />
            ) : (
              <input
                {...register('streetAddress')}
                autoComplete="street-address"
                className="register-input"
                placeholder="Street address *"
              />
            )}
          </Field>

          <Field label="City *" error={errors.city?.message}>
            <input
              {...register('city')}
              autoComplete="address-level2"
              className="register-input"
              placeholder="City *"
            />
          </Field>

          <Field label="Province *" error={errors.province?.message}>
            <select {...register('province')} autoComplete="address-level1" className="register-input">
              <option value="">Province *</option>
              {provinces.map((item) => (
                <option key={item.value} value={item.value}>
                  {item.label}
                </option>
              ))}
            </select>
          </Field>
        </div>
      </RegisterSection>

      <RegisterSection title="Security">
        <div className="register-grid">
          <Field label="Password *" error={errors.password?.message}>
            <div className="register-password-wrap">
              <input
                {...register('password')}
                type={showPassword ? 'text' : 'password'}
                className="register-input register-input-password"
                placeholder="Password *"
              />
              <button
                type="button"
                className="register-password-toggle"
                onClick={() => setShowPassword((value) => !value)}
                aria-label={showPassword ? 'Hide password' : 'Show password'}
              >
                {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                <span>{showPassword ? 'Hide' : 'Show'}</span>
              </button>
            </div>
            <p className="register-security-copy">
              Password must be at least 12 characters.
              <br />
              Include uppercase, lowercase, number, and special character.
            </p>
          </Field>

          <Field label="Confirm password *" error={errors.confirmPassword?.message}>
            <div className="register-password-wrap">
              <input
                {...register('confirmPassword')}
                type={showConfirmPassword ? 'text' : 'password'}
                className="register-input register-input-password"
                placeholder="Confirm password *"
              />
              <button
                type="button"
                className="register-password-toggle"
                onClick={() => setShowConfirmPassword((value) => !value)}
                aria-label={showConfirmPassword ? 'Hide confirm password' : 'Show confirm password'}
              >
                {showConfirmPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                <span>{showConfirmPassword ? 'Hide' : 'Show'}</span>
              </button>
            </div>
          </Field>
        </div>
      </RegisterSection>

      <div className="register-consent">
        <label className="register-checkbox">
          <input type="checkbox" {...register('acceptTerms')} />
          <span>I accept the Advertified <Link to="/terms-of-service" className="font-semibold text-brand underline">terms and conditions</Link> and confirm that I am 18 years or older.</span>
        </label>
        {errors.acceptTerms ? <p className="register-error">{errors.acceptTerms.message}</p> : null}

        <label className="register-checkbox">
          <input type="checkbox" {...register('acceptPopia')} />
          <span>I consent to POPIA-aligned processing for onboarding, verification, payment, campaign planning, and billing as described in the <Link to="/privacy" className="font-semibold text-brand underline">privacy policy</Link>.</span>
        </label>
        {errors.acceptPopia ? <p className="register-error">{errors.acceptPopia.message}</p> : null}
      </div>

      <button type="submit" disabled={loading} className="register-submit">
        {loading ? 'Creating account...' : 'Create account'}
      </button>

      <p className="register-footer-copy">Already registered? Check your activation email or browse FAQs.</p>
    </form>
  );
}

function RegisterSection({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <section className="register-section">
      <p className="register-section-title">{title}</p>
      {children}
    </section>
  );
}

function Field({
  label,
  error,
  className,
  children,
}: {
  label: string;
  error?: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <label className={['register-field', className].filter(Boolean).join(' ')}>
      <span className="sr-only">{label}</span>
      {children}
      {error ? <p className="register-error">{error}</p> : null}
    </label>
  );
}
