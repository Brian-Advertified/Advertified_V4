# Advertified V4

Advertified V4 is a full-stack campaign planning and activation platform with:

- `src/Advertified.Web`: Vite + React client workspace
- `src/Advertified.App`: ASP.NET Core API, campaign workflows, auth, payments, admin, and recommendation logic
- `src/Advertified.AIPlatform.*`: AI platform layers for creative generation and provider orchestration
- `tests/`: .NET test projects
- `database/`: schema/bootstrap scripts and operational seed data

## Local Development

### Web

```bash
cd src/Advertified.Web
npm install
npm run dev
```

Common commands:

```bash
npm run build
npm test
```

### API

```bash
dotnet build src/Advertified.App/Advertified.App.csproj
dotnet run --project src/Advertified.App/Advertified.App.csproj
```

#### Local secrets

- Copy `src/Advertified.App/appsettings.Development.example.json` to `src/Advertified.App/appsettings.Development.json`.
- Populate local API keys and database passwords without committing them.
- `src/Advertified.App/appsettings.Development.json` is ignored by `.gitignore`.

Common commands:

```bash
dotnet test tests/Advertified.App.Tests/Advertified.App.Tests.csproj
dotnet test tests/Advertified.AIPlatform.Tests/Advertified.AIPlatform.Tests.csproj
```

## High-Level Architecture

- Web uses React Router, React Query, and a typed service client in `src/Advertified.Web/src/services`.
- API uses controller endpoints backed by scoped services in `src/Advertified.App/Services`.
- Authentication is bearer-token based through `ISessionTokenService` and `ICurrentUserAccessor`.
- Campaign recommendation generation flows through `CampaignRecommendationService -> MediaPlanningEngine -> PlanningCandidateLoader -> inventory sources`.
- Public proposal approval runs through `PublicProposalController` and `RecommendationApprovalWorkflowService`.
- AI integrations are externalized behind HTTP clients and provider abstractions in `src/Advertified.App` and `src/Advertified.AIPlatform.*`.

## Operational Notes

- Runtime secrets belong in environment-specific configuration and should not be committed.
- Local generated files and test/build artifacts are ignored through `.gitignore`.
- `src/Advertified.App/appsettings.Development.json` is ignored for local credential overrides.
- Use environment variables or local secrets stores for API keys and database passwords rather than committing them.
- DEV deployment currently supports both workflow-based and manual EC2 deployment paths.

## Key Documentation

- `docs/SYSTEM_TRAINING_MANUAL.md`
- `docs/ADVERTIFIED_SYSTEM_HANDOVER_GUIDE.md`
- `DEPLOY_DEV_EC2.md`

