import { Navigate } from 'react-router-dom';
import type { AppRoute } from './routeUtils';
import { clientRoutes } from './clientRoutes';
import { opsRoutes } from './opsRoutes';
import { publicRoutes } from './publicRoutes';

export const appRoutes: AppRoute[] = [
  ...publicRoutes,
  ...clientRoutes,
  ...opsRoutes,
  { path: '*', element: <Navigate to="/" replace /> },
];
