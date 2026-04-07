import { zodResolver } from '@hookform/resolvers/zod';
import { Eye, EyeOff } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { useState } from 'react';
import type { LoginSchema } from '../schemas';
import { loginSchema } from '../schemas';

export function LoginForm({
  onSubmit,
  loading,
  initialEmail = '',
}: {
  onSubmit: (values: LoginSchema) => Promise<void>;
  loading: boolean;
  initialEmail?: string;
}) {
  const [showPassword, setShowPassword] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginSchema>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: initialEmail,
      password: '',
    },
  });

  return (
    <form className="panel flex flex-col gap-5 px-6 py-6 sm:px-8" onSubmit={handleSubmit(onSubmit)}>
      <div>
        <label className="label-base">Email</label>
        <input {...register('email')} className="input-base" placeholder="you@business.com" />
        {errors.email ? <p className="helper-text text-rose-600">{errors.email.message}</p> : null}
      </div>
      <div>
        <label className="label-base">Password</label>
        <div className="relative">
          <input
            {...register('password')}
            type={showPassword ? 'text' : 'password'}
            className="input-base pr-16"
            placeholder="Enter your password"
          />
          <button
            type="button"
            className="absolute right-4 top-1/2 inline-flex -translate-y-1/2 items-center gap-1 text-xs font-semibold text-ink-soft transition hover:text-ink"
            onClick={() => setShowPassword((current) => !current)}
            aria-label={showPassword ? 'Hide password' : 'Show password'}
          >
            {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
            <span>{showPassword ? 'Hide' : 'Show'}</span>
          </button>
        </div>
        {errors.password ? <p className="helper-text text-rose-600">{errors.password.message}</p> : null}
      </div>
      <button type="submit" disabled={loading} className="button-primary px-5 py-3 disabled:opacity-60">
        {loading ? 'Signing you in...' : 'Log in'}
      </button>
    </form>
  );
}
