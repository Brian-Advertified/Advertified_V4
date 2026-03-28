import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import type { LoginSchema } from '../schemas';
import { loginSchema } from '../schemas';

export function LoginForm({
  onSubmit,
  loading,
}: {
  onSubmit: (values: LoginSchema) => Promise<void>;
  loading: boolean;
}) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginSchema>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
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
        <input {...register('password')} type="password" className="input-base" placeholder="Enter your password" />
        {errors.password ? <p className="helper-text text-rose-600">{errors.password.message}</p> : null}
      </div>
      <button type="submit" disabled={loading} className="rounded-full bg-ink px-5 py-3 text-sm font-semibold text-white transition hover:bg-brand disabled:opacity-60">
        {loading ? 'Signing you in...' : 'Log in'}
      </button>
    </form>
  );
}
