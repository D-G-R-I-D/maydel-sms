using Maydel.Api.Common;
using Maydel.Application.Academics;
using Maydel.Application.Academics.AddTerm;
using Maydel.Application.Academics.CreateClassRoom;
using Maydel.Application.Academics.CreateSession;
using Maydel.Application.Academics.CreateSubject;
using Maydel.Application.Academics.GetCurrentTerm;
using Maydel.Application.Academics.ListClassLevels;
using Maydel.Application.Academics.ListClassRooms;
using Maydel.Application.Academics.ListClassSubjects;
using Maydel.Application.Academics.ListSessions;
using Maydel.Application.Academics.ListSubjects;
using Maydel.Application.Academics.MakeTermCurrent;
using Maydel.Application.Academics.SetClassSubjects;
using Maydel.Application.Academics.UpdateClassRoom;
using Maydel.Application.Academics.UpdateSession;
using Maydel.Application.Academics.UpdateSubject;
using Maydel.Application.Academics.UpdateTerm;
using Maydel.Application.Common.Messaging;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>School setup (E2.1–E2.3, E4.2): sessions & terms, classes, subjects. Staff read; only admins change.</summary>
public static class AcademicsEndpoints
{
    private const string Base = "/api/academics";

    public static void MapAcademicsEndpoints(this IEndpointRouteBuilder app)
    {
        var staff = app.MapGroup(Base).WithTags("Academics").RequireAuthorization(Policies.Staff);
        var admin = app.MapGroup(Base).WithTags("Academics").RequireAuthorization(Policies.Admin);

        // Sessions & terms
        staff.MapGet("/sessions", ListSessions).WithName("ListSessions").ProducesProblems(401, 403);
        app.MapGet($"{Base}/terms/current", GetCurrentTerm).WithTags("Academics").RequireAuthorization()
            .WithName("GetCurrentTerm").ProducesProblems(401, 404);
        admin.MapPost("/sessions", CreateSession).WithName("CreateSession").ProducesProblems(400, 401, 403, 409);
        admin.MapPut("/sessions/{id:guid}", UpdateSession).WithName("UpdateSession").ProducesProblems(400, 401, 403, 404, 409);
        admin.MapPost("/sessions/{id:guid}/terms", AddTerm).WithName("AddTerm").ProducesProblems(400, 401, 403, 404);
        admin.MapPut("/terms/{id:guid}", UpdateTerm).WithName("UpdateTerm").ProducesProblems(400, 401, 403, 404);
        admin.MapPost("/terms/{id:guid}/make-current", MakeTermCurrent).WithName("MakeTermCurrent").ProducesProblems(401, 403, 404);

        // Classes
        staff.MapGet("/class-levels", ListClassLevels).WithName("ListClassLevels").ProducesProblems(401, 403);
        staff.MapGet("/classrooms", ListClassRooms).WithName("ListClassRooms").ProducesProblems(401, 403);
        admin.MapPost("/classrooms", CreateClassRoom).WithName("CreateClassRoom").ProducesProblems(400, 401, 403, 409);
        admin.MapPut("/classrooms/{id:guid}", UpdateClassRoom).WithName("UpdateClassRoom").ProducesProblems(400, 401, 403, 404, 409);
        staff.MapGet("/classrooms/{id:guid}/subjects", ListClassSubjects).WithName("ListClassSubjects").ProducesProblems(401, 403, 404);
        admin.MapPut("/classrooms/{id:guid}/subjects", SetClassSubjects).WithName("SetClassSubjects").ProducesProblems(400, 401, 403, 404, 422);

        // Subjects
        staff.MapGet("/subjects", ListSubjects).WithName("ListSubjects").ProducesProblems(401, 403);
        admin.MapPost("/subjects", CreateSubject).WithName("CreateSubject").ProducesProblems(400, 401, 403, 409);
        admin.MapPut("/subjects/{id:guid}", UpdateSubject).WithName("UpdateSubject").ProducesProblems(400, 401, 403, 404, 409);
    }

    private static async Task<Results<Ok<IReadOnlyList<SessionDto>>, ProblemHttpResult>> ListSessions(
        IQueryHandler<ListSessionsQuery, IReadOnlyList<SessionDto>> handler, CancellationToken ct) =>
        (await handler.Handle(new ListSessionsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<TermDto>, ProblemHttpResult>> GetCurrentTerm(
        IQueryHandler<GetCurrentTermQuery, TermDto> handler, CancellationToken ct) =>
        (await handler.Handle(new GetCurrentTermQuery(), ct)).ToHttpResult();

    private static async Task<Results<Created<SessionDto>, ProblemHttpResult>> CreateSession(
        CreateSessionCommand command, ICommandHandler<CreateSessionCommand, SessionDto> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToCreatedResult(s => $"{Base}/sessions/{s.Id}");

    private static async Task<Results<Ok<SessionDto>, ProblemHttpResult>> UpdateSession(
        Guid id, SessionRequest body, ICommandHandler<UpdateSessionCommand, SessionDto> handler, CancellationToken ct) =>
        (await handler.Handle(new UpdateSessionCommand(id, body.Name, body.StartsOn, body.EndsOn), ct)).ToHttpResult();

    private static async Task<Results<Created<TermDto>, ProblemHttpResult>> AddTerm(
        Guid id, TermRequest body, ICommandHandler<AddTermCommand, TermDto> handler, CancellationToken ct) =>
        (await handler.Handle(new AddTermCommand(id, body.Number, body.StartsOn, body.EndsOn, body.MidTermStartsOn, body.MidTermEndsOn), ct))
            .ToCreatedResult(t => $"{Base}/terms/{t.Id}");

    private static async Task<Results<Ok<TermDto>, ProblemHttpResult>> UpdateTerm(
        Guid id, TermDatesRequest body, ICommandHandler<UpdateTermCommand, TermDto> handler, CancellationToken ct) =>
        (await handler.Handle(new UpdateTermCommand(id, body.StartsOn, body.EndsOn, body.MidTermStartsOn, body.MidTermEndsOn), ct)).ToHttpResult();

    private static async Task<Results<Ok<TermDto>, ProblemHttpResult>> MakeTermCurrent(
        Guid id, ICommandHandler<MakeTermCurrentCommand, TermDto> handler, CancellationToken ct) =>
        (await handler.Handle(new MakeTermCurrentCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ClassLevelDto>>, ProblemHttpResult>> ListClassLevels(
        IQueryHandler<ListClassLevelsQuery, IReadOnlyList<ClassLevelDto>> handler, CancellationToken ct) =>
        (await handler.Handle(new ListClassLevelsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ClassRoomDto>>, ProblemHttpResult>> ListClassRooms(
        IQueryHandler<ListClassRoomsQuery, IReadOnlyList<ClassRoomDto>> handler, CancellationToken ct) =>
        (await handler.Handle(new ListClassRoomsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Created<ClassRoomDto>, ProblemHttpResult>> CreateClassRoom(
        CreateClassRoomCommand command, ICommandHandler<CreateClassRoomCommand, ClassRoomDto> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToCreatedResult(r => $"{Base}/classrooms/{r.Id}");

    private static async Task<Results<Ok<ClassRoomDto>, ProblemHttpResult>> UpdateClassRoom(
        Guid id, ClassRoomUpdateRequest body, ICommandHandler<UpdateClassRoomCommand, ClassRoomDto> handler, CancellationToken ct) =>
        (await handler.Handle(new UpdateClassRoomCommand(id, body.Arm, body.Capacity, body.FormTeacherId), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ClassSubjectDto>>, ProblemHttpResult>> ListClassSubjects(
        Guid id, IQueryHandler<ListClassSubjectsQuery, IReadOnlyList<ClassSubjectDto>> handler, CancellationToken ct) =>
        (await handler.Handle(new ListClassSubjectsQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ClassSubjectDto>>, ProblemHttpResult>> SetClassSubjects(
        Guid id, ClassSubjectsRequest body, ICommandHandler<SetClassSubjectsCommand, IReadOnlyList<ClassSubjectDto>> handler, CancellationToken ct) =>
        (await handler.Handle(new SetClassSubjectsCommand(id, body.Subjects), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<SubjectDto>>, ProblemHttpResult>> ListSubjects(
        IQueryHandler<ListSubjectsQuery, IReadOnlyList<SubjectDto>> handler, CancellationToken ct) =>
        (await handler.Handle(new ListSubjectsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Created<SubjectDto>, ProblemHttpResult>> CreateSubject(
        CreateSubjectCommand command, ICommandHandler<CreateSubjectCommand, SubjectDto> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToCreatedResult(s => $"{Base}/subjects/{s.Id}");

    private static async Task<Results<Ok<SubjectDto>, ProblemHttpResult>> UpdateSubject(
        Guid id, SubjectRequest body, ICommandHandler<UpdateSubjectCommand, SubjectDto> handler, CancellationToken ct) =>
        (await handler.Handle(new UpdateSubjectCommand(id, body.Name, body.Code, body.IsActive), ct)).ToHttpResult();
}
