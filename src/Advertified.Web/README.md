# Advertified Web

Advertified Web is the React + TypeScript frontend for the Advertified platform. It serves the public marketing site plus the authenticated client, agent, creative, and admin workspaces.

## Stack

- React 19
- TypeScript
- Vite
- React Router
- TanStack React Query

## Main Areas

- Public routes: marketing pages, package selection, checkout, login, registration, email verification
- Client routes: dashboard, orders, single campaign workspace
- Agent routes: dashboard, briefs, recommendations, approvals, messaging, tasks, campaign operations
- Creative routes: dashboard and studio workflow
- Admin routes: dashboard, operations, audit, imports, pricing, geography, stations, engine, monitoring, users

## Local Development

Run the frontend from `src/Advertified.Web`:

```bash
npm install
npm run dev
```

Default local frontend URL:

```text
http://localhost:5173
```

The frontend expects the API to be available separately. In local development the backend is typically served from the `src/Advertified.App` project.

## Scripts

```bash
npm run dev
npm run build
npm run lint
npm run preview
```

## Architecture Notes

### Routing

- The top-level router lives in `src/app/App.tsx`
- Major routes are lazy-loaded so public, client, agent, creative, and admin sections do not all ship in the initial bundle
- `ProtectedRoute` is a UX guard only; real authorization is enforced by the backend

### Auth Session

- The app uses `AuthProvider` from `src/features/auth/auth-context.tsx`
- Session state is persisted in browser storage and attached to API requests through the centralized client in `src/services/advertifiedApi.ts`
- Backend auth now uses signed session tokens rather than caller-supplied user IDs

### Data Fetching

- Server state is managed with React Query
- API calls are centralized in `src/services/advertifiedApi.ts`
- Shared domain contracts live in `src/types/domain.ts`

### Error Handling

- The app root is wrapped in `AppErrorBoundary`
- Route chunks render through `Suspense` with `LoadingState`
- Individual screens are still responsible for query-specific loading, empty, and error states

## Key Product Workflows

### Client

- Browse packages
- Select a campaign budget
- Checkout and view order history
- Review one campaign workspace for approvals, messages, progress, and final creative sign-off

### Agent

- Manage briefs and campaigns
- Build and send recommendations
- Handle approvals and client messages
- Mark campaigns live after final approval

### Creative

- Pick up approved campaigns
- Work in the creative studio
- Send finished media back to the client for approval

### Admin

- Manage stations, pricing, users, imports, monitoring, and audit surfaces
- Process refunds
- Pause and resume campaigns
- Review campaign operations state

## Consent, Messaging, and Pricing Notes

- Consent preferences are persisted through the backend, not just browser storage
- Campaign messaging is a persisted conversation per campaign with role-based access
- Client-facing recommendation views and PDFs show only overall totals, not line-item pricing
- Checkout totals include the hidden AI Studio reserve in the overall amount rather than as a visible surcharge

## Folder Guide

```text
src/
  app/                  App shell and routing
  components/           Shared UI and layout primitives
  features/             Domain-oriented UI and state modules
  lib/                  Shared helpers and access logic
  pages/                Route screens grouped by role
  services/             API client and transport helpers
  types/                Shared TypeScript contracts
```

## Current Gaps

- Frontend test coverage is not in place yet
- Some large route files still need to be split into smaller feature modules
- Notification aggregation and some campaign orchestration logic still need further cleanup

## Build Verification

Type-check the app with:

```bash
npx tsc -b
```

Create a production bundle with:

```bash
npm run build
```
