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
  email: z.string().email('Please enter a valid email.'),
  password: z.string().min(1, 'Password is required.'),
});

type LoginForm = z.infer<typeof schema>;

export const LoginPage = () => {
  const navigate = useNavigate();
  const { setAuth } = useAuthStore();
  const [error, setError] = useState<string | null>(null);

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<LoginForm>({
    resolver: zodResolver(schema),
  });

  const onSubmit = async (values: LoginForm) => {
    setError(null);
    try {
      const { data } = await api.post<AuthResult>('/auth/login', values);
      setAuth({ userId: data.userId, email: data.email, fullName: data.fullName }, data.accessToken);
      navigate('/');
    } catch {
      setError('Invalid email or password.');
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-900 via-gray-800 to-brand-900 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        {/* Logo */}
        <div className="flex items-center gap-3 justify-center mb-8">
          <div className="h-10 w-10 rounded-xl bg-brand-500 flex items-center justify-center text-white font-bold text-lg">F</div>
          <span className="text-2xl font-bold text-white">FinSight</span>
        </div>

        <div className="bg-white rounded-2xl shadow-2xl p-8">
          <h1 className="text-xl font-semibold text-gray-900 mb-1">Welcome back</h1>
          <p className="text-sm text-gray-500 mb-6">Sign in to your account</p>

          {error && (
            <div className="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <Input label="Email" type="email" autoComplete="email" error={errors.email?.message} {...register('email')} />
            <Input label="Password" type="password" autoComplete="current-password" error={errors.password?.message} {...register('password')} />

            <Button type="submit" className="w-full justify-center" isLoading={isSubmitting}>
              Sign in
            </Button>
          </form>

          <p className="mt-6 text-center text-sm text-gray-500">
            Don't have an account?{' '}
            <Link to="/register" className="font-medium text-brand-600 hover:text-brand-700">Create one</Link>
          </p>

          <div className="mt-4 rounded-lg bg-gray-50 px-4 py-3 text-xs text-gray-500">
            <strong>Demo:</strong> demo@finsight.dev / Test@Password1!
          </div>
        </div>
      </div>
    </div>
  );
};
