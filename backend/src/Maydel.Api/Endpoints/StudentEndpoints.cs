using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Application.Students;
using Maydel.Application.Students.AddGuardian;
using Maydel.Application.Students.ChangeStudentStatus;
using Maydel.Application.Students.CreateStudent;
using Maydel.Application.Students.GetGuardian;
using Maydel.Application.Students.GetStudent;
using Maydel.Application.Students.IssueGuardianLogin;
using Maydel.Application.Students.IssueStudentLogin;
using Maydel.Application.Students.RemoveGuardian;
using Maydel.Application.Students.SearchGuardians;
using Maydel.Application.Students.SearchStudents;
using Maydel.Application.Students.UpdateGuardian;
using Maydel.Application.Students.UpdateGuardianLink;
using Maydel.Application.Students.UpdateStudent;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>Students, guardians and their logins (E3.1–E3.5). Staff read students; only admins change anything.</summary>
public static class StudentEndpoints
{
    public static void MapStudentEndpoints(this IEndpointRouteBuilder app)
    {
        var staff = app.MapGroup("/api/students").WithTags("Students").RequireAuthorization(Policies.Staff);
        var admin = app.MapGroup("/api/students").WithTags("Students").RequireAuthorization(Policies.Admin);

        staff.MapGet("", Search).WithName("SearchStudents").ProducesProblems(400, 401, 403);
        staff.MapGet("/{id:guid}", Get).WithName("GetStudent").ProducesProblems(401, 403, 404);
        admin.MapPost("", Create).WithName("CreateStudent").ProducesProblems(400, 401, 403, 422);
        admin.MapPut("/{id:guid}", Update).WithName("UpdateStudent").ProducesProblems(400, 401, 403, 404, 422);
        admin.MapPost("/{id:guid}/status", ChangeStatus).WithName("ChangeStudentStatus").ProducesProblems(400, 401, 403, 404, 422);
        admin.MapPost("/{id:guid}/login", IssueStudentLogin).WithName("IssueStudentLogin").ProducesProblems(401, 403, 404, 422);
        admin.MapPost("/{id:guid}/guardians", AddGuardian).WithName("AddGuardian").ProducesProblems(400, 401, 403, 404, 409);
        admin.MapPut("/{id:guid}/guardians/{guardianId:guid}", UpdateGuardianLink).WithName("UpdateGuardianLink").ProducesProblems(400, 401, 403, 404);
        admin.MapDelete("/{id:guid}/guardians/{guardianId:guid}", RemoveGuardian).WithName("RemoveGuardian").ProducesProblems(401, 403, 404);

        var guardians = app.MapGroup("/api/guardians").WithTags("Guardians").RequireAuthorization(Policies.Admin);
        guardians.MapGet("", SearchGuardians).WithName("SearchGuardians").ProducesProblems(400, 401, 403);
        guardians.MapGet("/{id:guid}", GetGuardian).WithName("GetGuardian").ProducesProblems(401, 403, 404);
        guardians.MapPut("/{id:guid}", UpdateGuardian).WithName("UpdateGuardian").ProducesProblems(400, 401, 403, 404, 409);
        guardians.MapPost("/{id:guid}/login", IssueGuardianLogin).WithName("IssueGuardianLogin").ProducesProblems(401, 403, 404, 409);
    }

    private static async Task<Results<Ok<PagedResult<StudentListItem>>, ProblemHttpResult>> Search(
        [AsParameters] SearchStudentsQuery query, IQueryHandler<SearchStudentsQuery, PagedResult<StudentListItem>> handler, CancellationToken ct) =>
        (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<StudentDetail>, ProblemHttpResult>> Get(
        Guid id, IQueryHandler<GetStudentQuery, StudentDetail> handler, CancellationToken ct) =>
        (await handler.Handle(new GetStudentQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Created<StudentDetail>, ProblemHttpResult>> Create(
        StudentRequest b, ICommandHandler<CreateStudentCommand, StudentDetail> handler, CancellationToken ct) =>
        (await handler.Handle(new CreateStudentCommand(b.FirstName, b.MiddleName, b.LastName, b.DateOfBirth, b.Sex, b.StateOfOrigin, b.Lga,
            b.HomeAddress, b.House, b.MedicalNotes, b.ClassRoomId, b.AdmittedOn), ct)).ToCreatedResult(s => $"/api/students/{s.Id}");

    private static async Task<Results<Ok<StudentDetail>, ProblemHttpResult>> Update(
        Guid id, StudentRequest b, ICommandHandler<UpdateStudentCommand, StudentDetail> handler, CancellationToken ct) =>
        (await handler.Handle(new UpdateStudentCommand(id, b.FirstName, b.MiddleName, b.LastName, b.DateOfBirth, b.Sex, b.StateOfOrigin, b.Lga,
            b.HomeAddress, b.House, b.MedicalNotes, b.ClassRoomId, b.AdmittedOn), ct)).ToHttpResult();

    private static async Task<Results<Ok<StudentDetail>, ProblemHttpResult>> ChangeStatus(
        Guid id, StudentStatusRequest body, ICommandHandler<ChangeStudentStatusCommand, StudentDetail> handler, CancellationToken ct) =>
        (await handler.Handle(new ChangeStudentStatusCommand(id, body.Status), ct)).ToHttpResult();

    private static async Task<Results<Ok<IssuedCredentials>, ProblemHttpResult>> IssueStudentLogin(
        Guid id, ICommandHandler<IssueStudentLoginCommand, IssuedCredentials> handler, CancellationToken ct) =>
        (await handler.Handle(new IssueStudentLoginCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<GuardianAdded>, ProblemHttpResult>> AddGuardian(
        Guid id, GuardianRequest b, ICommandHandler<AddGuardianCommand, GuardianAdded> handler, CancellationToken ct) =>
        (await handler.Handle(new AddGuardianCommand(id, b.FullName, b.Phone, b.Email, b.Occupation, b.Address, b.Relationship, b.IsPrimaryContact, b.CanPickUp), ct))
            .ToHttpResult();

    private static async Task<Results<Ok<StudentDetail>, ProblemHttpResult>> UpdateGuardianLink(
        Guid id, Guid guardianId, GuardianLinkRequest b, ICommandHandler<UpdateGuardianLinkCommand, StudentDetail> handler, CancellationToken ct) =>
        (await handler.Handle(new UpdateGuardianLinkCommand(id, guardianId, b.Relationship, b.IsPrimaryContact, b.CanPickUp), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveGuardian(
        Guid id, Guid guardianId, ICommandHandler<RemoveGuardianCommand> handler, CancellationToken ct) =>
        (await handler.Handle(new RemoveGuardianCommand(id, guardianId), ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<GuardianListItem>>, ProblemHttpResult>> SearchGuardians(
        [AsParameters] SearchGuardiansQuery query, IQueryHandler<SearchGuardiansQuery, PagedResult<GuardianListItem>> handler, CancellationToken ct) =>
        (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<GuardianDetail>, ProblemHttpResult>> GetGuardian(
        Guid id, IQueryHandler<GetGuardianQuery, GuardianDetail> handler, CancellationToken ct) =>
        (await handler.Handle(new GetGuardianQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<GuardianDetail>, ProblemHttpResult>> UpdateGuardian(
        Guid id, GuardianUpdateRequest b, ICommandHandler<UpdateGuardianCommand, GuardianDetail> handler, CancellationToken ct) =>
        (await handler.Handle(new UpdateGuardianCommand(id, b.FullName, b.Phone, b.Email, b.Occupation, b.Address), ct)).ToHttpResult();

    private static async Task<Results<Ok<GuardianLoginIssued>, ProblemHttpResult>> IssueGuardianLogin(
        Guid id, ICommandHandler<IssueGuardianLoginCommand, GuardianLoginIssued> handler, CancellationToken ct) =>
        (await handler.Handle(new IssueGuardianLoginCommand(id), ct)).ToHttpResult();
}
