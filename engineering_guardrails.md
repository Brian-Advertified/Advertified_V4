# Engineering Guardrails

## Purpose
This document defines how we write, review, and ship code. Its goal is to keep the codebase maintainable, predictable, and production-safe.

We optimize for:
- clarity over cleverness
- consistency over personal preference
- small safe changes over large risky rewrites
- reusable systems over repeated logic
- complete implementations over half-finished code

---

## Non-Negotiable Rules

### 1. Follow SOLID principles
All new code should respect the following:

- **Single Responsibility**: a class, service, or function should have one reason to change.
- **Open/Closed**: extend behavior without editing stable core logic when possible.
- **Liskov Substitution**: derived implementations must behave like their abstractions.
- **Interface Segregation**: prefer small focused interfaces over large multipurpose ones.
- **Dependency Inversion**: depend on abstractions, not concretions.

### 2. Do not repeat logic
Before adding new code, check whether the same logic already exists.

Rules:
- do not copy and paste business logic
- extract shared logic into services, helpers, components, or utilities
- centralize constants, enums, validation rules, and mappings
- if logic appears more than once, stop and refactor

### 3. No hanging or incomplete code
Never leave behind code that is partially implemented or misleading.

Do not commit:
- dead code
- commented-out blocks of old logic
- placeholder methods without a tracked reason
- unused variables, classes, imports, endpoints, or components
- TODOs without context and ownership
- incomplete branches that appear production-ready but are not

If work is intentionally incomplete, it must be:
- behind a feature flag, or
- clearly marked with a tracked ticket/reference, or
- kept out of the main branch

### 4. Keep methods and classes small
Use these defaults unless there is a good reason not to:
- methods should do one thing
- classes should have one role
- files should be easy to scan quickly
- avoid deep nesting
- prefer early returns over complex branching

### 5. Prefer explicit code over magic
- use clear names
- make side effects obvious
- avoid hidden state
- avoid surprising behavior
- avoid overly clever abstractions

### 6. Every change must leave the codebase cleaner
When touching existing code:
- improve names where needed
- remove obvious dead code
- reduce duplication if nearby
- add or improve tests if behavior matters
- do not leave messy areas worse than you found them

---

## Coding Standards

### Naming
- Use names that describe intent, not implementation detail.
- Prefer business language over vague technical names.
- Avoid names like `data`, `temp`, `helper`, `manager`, `stuff`, `misc`.
- Booleans must read clearly, for example: `isActive`, `hasErrors`, `canPublish`.

### Functions
A good function:
- has one responsibility
- has a clear name
- has few parameters
- avoids hidden side effects
- returns predictable output

Avoid functions that:
- validate, transform, save, notify, and log all in one place
- mix domain logic with infrastructure concerns
- depend on unrelated global state

### Classes and Services
A good class:
- owns one responsibility
- exposes a small public surface
- hides internal details
- depends on interfaces where useful

Avoid classes that become:
- god objects
- dumping grounds for unrelated methods
- thin wrappers with no value

### Error Handling
- fail clearly and early
- never swallow exceptions silently
- return meaningful errors
- log with enough context to debug
- do not use exceptions for normal control flow

### Validation
- validate inputs at boundaries
- keep validation rules centralized where possible
- do not duplicate validation across layers unless intentionally defensive

### Configuration
- no magic strings where constants belong
- no hardcoded secrets, tokens, or environment-specific values
- all environment-specific behavior must come from configuration

---

## Architecture Rules

### Separation of Concerns
Keep these concerns separated:
- presentation/UI
- application orchestration
- domain/business rules
- infrastructure and external services
- persistence/data access

### Domain Logic
Business rules must not be scattered.

Rules:
- keep domain decisions in domain services or clearly owned modules
- controllers/endpoints should coordinate, not think
- repositories should fetch/store, not contain business policy
- UI should display state, not implement business rules

### Dependency Direction
Preferred direction:
- outer layers depend on inner abstractions
- business logic should not depend directly on framework-specific details unless unavoidable

### Reuse
Before adding new modules, ask:
- does this already exist?
- can this be extended safely?
- should this be extracted into a shared utility/service/component?

---

## Anti-Patterns We Avoid

Do not introduce:
- copy-paste programming
- giant service classes
- giant controllers
- utility files full of unrelated methods
- boolean parameter overloads that change behavior drastically
- nested condition pyramids
- hidden database calls inside loops where batching is needed
- premature abstraction without repeated use
- framework-specific code leaking everywhere
- silent fallback behavior that hides bugs

---

## Definition of Done
A task is not done unless all of the following are true:

- code builds successfully
- tests pass
- no dead or unused code was introduced
- no obvious duplication remains
- naming is clear
- edge cases are handled reasonably
- logs/errors are meaningful
- documentation is updated if behavior changed
- feature flags are used if rollout is incomplete
- reviewer can understand the change quickly

---

## Pull Request Checklist
Before opening a PR, confirm:

- [ ] I checked for existing logic before creating new logic
- [ ] I removed unused imports, code paths, and temporary code
- [ ] I kept responsibilities separated
- [ ] I did not mix business logic with transport, UI, or persistence unnecessarily
- [ ] I used clear naming
- [ ] I handled errors intentionally
- [ ] I added or updated tests where appropriate
- [ ] I verified there are no hanging TODOs without context
- [ ] I considered whether this change should be behind a feature flag
- [ ] I would be comfortable maintaining this code in 12 months

---

## Review Rules for Humans and AI Agents
Every reviewer, including AI coding tools, should check:

### Design
- Is this the simplest correct design?
- Does each unit have one responsibility?
- Are abstractions justified?
- Is there duplication that should be extracted?

### Safety
- Could this break existing flows?
- Are null, empty, failure, and timeout cases handled?
- Are external calls retried or guarded appropriately?

### Maintainability
- Can a new engineer understand this quickly?
- Are names obvious?
- Is the change easy to test?
- Does the code create future cleanup debt?

### Completeness
- Is any code path half-finished?
- Are there placeholders pretending to be complete?
- Are there branches with no implementation or unreachable states?

---

## AI Coding Agent Instructions
When using AI tools such as Codex, Cursor, or code assistants, the agent must follow these rules:

1. Read relevant files before editing.
2. Reuse existing patterns unless they are clearly wrong.
3. Do not create duplicate services, helpers, DTOs, or components.
4. Do not leave commented-out code.
5. Do not invent fake implementations just to satisfy a compile error.
6. If a task is incomplete, stop at a clean boundary and state what remains.
7. Make the smallest safe change that solves the problem.
8. Preserve backward compatibility unless the task explicitly allows breaking changes.
9. Update tests and docs when behavior changes.
10. Before finishing, perform a cleanup pass for dead code, duplication, and naming.

### AI Output Standard
Every AI-generated change should be able to answer:
- What changed?
- Why was it changed this way?
- What existing logic was reused?
- What duplication was avoided?
- What risks remain?

---

## TODO Policy
TODOs are allowed only when they are actionable.

Format:
`TODO(PROJ-123): Short concrete reason and next action.`

Bad:
- `TODO: fix later`
- `TODO: improve this`

Good:
- `TODO(ADV-214): Replace temporary inventory scoring with source-weighted scoring once supplier reliability metrics are available.`

If there is no ticket or owner, do not leave a TODO.

---

## Testing Expectations
Test behavior that matters.

Prioritize tests for:
- business rules
- scoring logic
- recommendation logic
- transformations and mappings
- failure paths
- integration boundaries

Avoid brittle tests that only mirror implementation details.

---

## Logging and Observability
Logs should help answer:
- what happened
- where it happened
- why it failed
- what entity/request was involved

Rules:
- include identifiers where useful
- never log secrets or sensitive values
- prefer structured logs where supported
- distinguish user errors from system failures

---

## Refactoring Rule
If you touch code and notice one of these, fix it if reasonably in scope:
- duplicate logic nearby
- unclear naming
- dead code
- long method doing multiple jobs
- repeated validation rules
- repeated mapping logic

Small cleanup is part of delivery, not separate optional work.

---

## Final Standard
We do not ship code that is:
- duplicated
- ambiguous
- half-finished
- misleading
- unnecessarily complex

We ship code that is:
- clear
- complete
- testable
- maintainable
- intentional

If a proposed change does not meet this standard, it is not ready.

