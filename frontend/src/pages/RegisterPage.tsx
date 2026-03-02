import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useNavigate, Link } from 'react-router-dom';
import { useState } from 'react';
import { api } from '../services/api';
import { useAuthStore } from '../store/authStore';
import { Input } from '../components/ui/Input';
import { Button } from '../components/ui/Button';
import type { AuthResult } from '../types';

const schema = z.object({
  firstName: z.string().min(1, 'First name is required.'),
  lastName: z.string().min(1, 'Last name is required.'),
  email: z.string().email('Please enter a valid email.'),
  password: z.string()
    .min(8, 'Password must be at least 8 characters.')
    .regex(/[A-Z]/, 'Must include an uppercase letter.')
    .regex(/[0-9]/, 'Must include a number.')
    .regex(/[^a-zA-Z0-9]/, 'Must include a special character.'),
});

type RegisterForm = z.infer<typeof schema>;

export const RegisterPage = () => {
  const navigate = useNavigate();
  const { setAuth } = useAuthStore();
  const [error, setError] = useState<string | null>(null);

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<RegisterForm>({
    resolver: zodResolver(schema),
  });

  const onSubmit = async (values: RegisterForm) => {
    setError(null);
    try {
      const { data } = await api.post<AuthResult>('/auth/register', values);
      setAuth({ userId: data.userId, email: data.email, fullName: data.fullName }, data.accessToken);
      navigate('/');
    } catch (err: any) {
      setError(err.response?.data?.detail ?? 'Registration failed. Please try again.');
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-900 via-gray-800 to-brand-900 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        <div className="flex items-center gap-3 justify-center mb-8">
          <div className="h-10 w-10 rounded-xl bg-brand-500 flex items-center justify-center text-white font-bold text-lg">F</div>
          <span className="text-2xl font-bold text-white">FinSight</span>
        </div>
        <div className="bg-white rounded-2xl shadow-2xl p-8">
          <h1 className="text-xl font-semibold text-gray-900 mb-1">Create your account</h1>
          <p className="text-sm text-gray-500 mb-6">Start tracking your finances today</p>

          {error && (
            <div className="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">{error}</div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <Input label="First name" error={errors.firstName?.message} {...register('firstName')} />
              <Input label="Last name" error={errors.lastName?.message} {...register('lastName')} />
            </div>
            <Input label="Email" type="email" error={errors.email?.message} {...register('email')} />
            <Input label="Password" type="password" error={errors.password?.message} {...register('password')} />
            <Button type="submit" className="w-full justify-center" isLoading={isSubmitting}>
              Create account
            </Button>
          </form>

          <p className="mt-6 text-center text-sm text-gray-500">
            Already have an account?{' '}
            <Link to="/login" className="font-medium text-brand-600 hover:text-brand-700">Sign in</Link>
          </p>
        </div>
      </div>
    </div>
  );
};
