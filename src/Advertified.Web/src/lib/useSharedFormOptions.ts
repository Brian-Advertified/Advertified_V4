import { useQuery } from '@tanstack/react-query';
import { advertifiedApi } from '../services/advertifiedApi';

export function useSharedFormOptions() {
  return useQuery({
    queryKey: ['public-form-options'],
    queryFn: advertifiedApi.getFormOptions,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}
