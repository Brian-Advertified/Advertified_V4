import { z } from 'zod';

const strongPasswordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{12,}$/;
const registrationNumberRegex = /^\d{4}\/\d{6,7}\/\d{2}$/;
const saIdRegex = /^\d{13}$/;
const iso2Regex = /^[A-Z]{2}$/;

export const registrationSchema = z
  .object({
    fullName: z
      .string()
      .min(1, 'Full name is required.')
      .refine((value) => value.trim().split(/\s+/).length >= 2, 'Enter your name and surname.'),
    email: z.email('Enter a valid email address.'),
    phone: z.string().min(1, 'Phone number is required.'),
    isSouthAfricanCitizen: z.boolean(),
    password: z
      .string()
      .regex(
        strongPasswordRegex,
        'Password must be at least 12 characters and include uppercase, lowercase, a number, and a special character.',
      ),
    confirmPassword: z.string(),
    businessName: z.string().min(1, 'Business name is required.'),
    businessType: z.string().min(1, 'Business type is required.'),
    registrationNumber: z
      .string()
      .regex(registrationNumberRegex, 'Registration number must look like 2024/123456/07.'),
    vatNumber: z.string().optional(),
    industry: z.string().min(1, 'Industry is required.'),
    annualRevenueBand: z.string().min(1, 'Annual revenue band is required.'),
    tradingAsName: z.string().optional(),
    streetAddress: z.string().min(1, 'Street address is required.'),
    city: z.string().min(1, 'City is required.'),
    province: z.string().min(1, 'Province is required.'),
    saIdNumber: z.string().optional(),
    passportNumber: z.string().optional(),
    passportCountryIso2: z.string().optional(),
    passportIssueDate: z.string().optional(),
    passportValidUntil: z.string().optional(),
    acceptTerms: z.boolean().refine((value) => value, 'You must accept the terms.'),
    acceptPopia: z.boolean().refine((value) => value, 'You must consent to POPIA processing.'),
  })
  .superRefine((value, ctx) => {
    if (value.confirmPassword !== value.password) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['confirmPassword'],
        message: 'Passwords do not match.',
      });
    }

    if (value.isSouthAfricanCitizen) {
      if (!value.saIdNumber || !saIdRegex.test(value.saIdNumber)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['saIdNumber'],
          message: 'SA ID number must be 13 digits.',
        });
      }
    } else {
      if (!value.passportNumber) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['passportNumber'],
          message: 'Passport number is required.',
        });
      }

      if (!value.passportCountryIso2 || !iso2Regex.test(value.passportCountryIso2.toUpperCase())) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['passportCountryIso2'],
          message: 'Enter a valid ISO-2 country code.',
        });
      }

      const issueDate = value.passportIssueDate ? new Date(value.passportIssueDate) : null;
      const validUntil = value.passportValidUntil ? new Date(value.passportValidUntil) : null;
      const now = new Date();

      if (!issueDate || Number.isNaN(issueDate.getTime())) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['passportIssueDate'],
          message: 'Passport issue date is required.',
        });
      }

      if (!validUntil || Number.isNaN(validUntil.getTime())) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['passportValidUntil'],
          message: 'Passport valid until date is required.',
        });
      }

      if (issueDate && validUntil && validUntil <= issueDate) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['passportValidUntil'],
          message: 'Passport valid until must be after issue date.',
        });
      }

      if (validUntil && validUntil <= now) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['passportValidUntil'],
          message: 'Passport must still be valid.',
        });
      }
    }
  });

export const loginSchema = z.object({
  email: z.email('Enter a valid email address.'),
  password: z.string().min(1, 'Password is required.'),
});

export type RegistrationSchema = z.infer<typeof registrationSchema>;
export type LoginSchema = z.infer<typeof loginSchema>;
