# ADR-004 — Code organisation: Clean Architecture + vertical slices (CQRS handlers, Result pattern, TypedResults)

- **Status:** Accepted (2026-10-12) — supersedes the draft "one `<Module>Endpoints.cs` file per module" layout.

## Context
The first backend draft put all DTOs, validation and logic for a module into one endpoints file. That is quick, but files grow to 300–500 lines, business logic sits next to HTTP plumbing, and handlers can't be unit-tested without HTTP. We also considered the opposite: classic layering, with a file per type spread across folders (`Dtos/`, `Repositories/`, `Services/`, `Controllers/`). There, one feature change touches 6+ folders, and generic repositories over EF Core just duplicate what `DbContext` already does.

## Decision

### Projects (dependencies point inward)
```
Maydel.Domain          entities, value objects, enums, domain rules & errors        (no dependencies)
Maydel.Application     use cases (commands/queries + handlers + validators + DTOs),
                       abstractions: IAppDbContext, ICurrentUser, IFileStorage, IEmailSender, IPaymentGateway
Maydel.Infrastructure  EF Core (AppDbContext implements IAppDbContext), Identity, storage, email, Paystack
Maydel.Api             HTTP only: endpoint mapping, auth policies, problem-details, composition root
```

### Vertical slices inside Application: one folder per use case
```
Maydel.Application/
  Students/
    CreateStudent/
      CreateStudentCommand.cs      // request record
      CreateStudentValidator.cs    // FluentValidation rules
      CreateStudentHandler.cs      // the use case; returns Result<StudentDetail>
    GetStudent/
      GetStudentQuery.cs
      GetStudentHandler.cs
    SearchStudents/ ...
    StudentDtos.cs                 // read models shared by this module's slices
  Assessment/
    SaveGradeSheet/ ...
    PublishReports/ ...
Maydel.Api/
  Endpoints/
    StudentEndpoints.cs            // ~5 lines per route: bind → handler → TypedResults
```
To change a feature, you open one folder. Entities stay in `Domain` because several slices share them.

### CQRS (without MediatR)
- **Commands** change state and return `Result` / `Result<T>`. **Queries** never change state: they use `AsNoTracking` and project straight to DTOs.
- Handlers implement `ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>`. They're registered by assembly scanning and injected straight into endpoints.
- Cross-cutting concerns are decorators on the handler interfaces: validation, logging, and a transaction wrapper for commands.
- We deliberately don't use MediatR. It moved to a commercial licence in 2025, and it adds indirection we don't need. If the team prefers a mediator later, swapping it in is mechanical.

### No repository layer over EF Core
`DbContext` already provides a unit of work and repositories (`DbSet<T>`). Handlers use `IAppDbContext`. When a complex query is reused, it becomes a named extension method or query object in that module, not a generic `IRepository<T>`.

### Result pattern + TypedResults
- Expected failures (validation, not found, forbidden, conflict, business rule) are **returned** as `Result.Failure(Error)` and not thrown. `Error` has a `Type` (Validation, NotFound, Forbidden, Conflict, Rule), a code and a message.
- Exceptions are reserved for genuinely unexpected faults. The global handler turns them into a 500 problem response.
- Endpoints return `TypedResults` with union return types (as implemented: `Results<Ok<T>, ProblemHttpResult>` plus declared error statuses — see ADR-005 §4), e.g. `Task<Results<Ok<StudentDetail>, NotFound<ProblemDetails>, ValidationProblem>>`. Compared with `Results.Ok(...)`, this gives compile-time checking of the possible responses, accurate OpenAPI metadata generated automatically (so the frontend's generated types are correct), and endpoints that can be unit-tested without HTTP.
- One central extension maps `Error.Type` → status code + RFC 9457 problem details (`result.ToHttpResult()`).

### Validation
FluentValidation (Apache-2.0) for request validators, run by the validation decorator. Invariants that must always hold are also enforced inside domain entities.

## Consequences
- + Every use case is small, discoverable and testable on its own: test the handler with a Testcontainers DB, and the endpoint with WebApplicationFactory.
- + The OpenAPI contract is precise, so the generated TypeScript client is precise too.
- − More files (3–4 per use case) and a little boilerplate. That's acceptable, and there's a `dotnet new` item template for slices to cut the typing (task in E0).
- The draft endpoint code gets refactored into this shape as part of Phase 0/1. Its logic (access rules, report calculation, token service) carries over unchanged.
