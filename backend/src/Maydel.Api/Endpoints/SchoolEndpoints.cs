using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Maydel.Application.SchoolSettings;
using Maydel.Application.SchoolSettings.GetSchoolImage;
using Maydel.Application.SchoolSettings.GetSchoolProfile;
using Maydel.Application.SchoolSettings.RemoveSchoolImage;
using Maydel.Application.SchoolSettings.SetSchoolImage;
using Maydel.Application.SchoolSettings.UpdateSchoolProfile;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>
/// School profile (E2.6): details and images printed on report cards. Staff read it; admins change it. The logo is
/// public (it's on the website too); the principal's signature is admin-only, since it could be misused if copied.
/// </summary>
public static class SchoolEndpoints
{
    public static void MapSchoolEndpoints(this IEndpointRouteBuilder app)
    {
        var staff = app.MapGroup("/api/school").WithTags("School").RequireAuthorization(Policies.Staff);
        var admin = app.MapGroup("/api/school").WithTags("School").RequireAuthorization(Policies.Admin);
        var open = app.MapGroup("/api/public/school").WithTags("School").AllowAnonymous();

        staff.MapGet("/profile", Get).WithName("GetSchoolProfile").ProducesProblems(401, 403);
        admin.MapPut("/profile", Update).WithName("UpdateSchoolProfile").ProducesProblems(400, 401, 403);
        admin.MapPut("/profile/logo", SetLogo).WithName("SetSchoolLogo").Upload();
        admin.MapDelete("/profile/logo", RemoveLogo).WithName("RemoveSchoolLogo").ProducesProblems(401, 403);
        admin.MapGet("/profile/signature", GetSignature).WithName("GetPrincipalSignature").ProducesProblems(401, 403, 404).PrivateCache();
        admin.MapPut("/profile/signature", SetSignature).WithName("SetPrincipalSignature").Upload();
        admin.MapDelete("/profile/signature", RemoveSignature).WithName("RemovePrincipalSignature").ProducesProblems(401, 403);
        open.MapGet("/logo", GetLogo).WithName("GetSchoolLogo").ProducesProblems(404).PrivateCache();
    }

    private static async Task<Results<Ok<SchoolProfileDto>, ProblemHttpResult>> Get(
        IQueryHandler<GetSchoolProfileQuery, SchoolProfileDto> handler, CancellationToken ct) =>
        (await handler.Handle(new GetSchoolProfileQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<SchoolProfileDto>, ProblemHttpResult>> Update(
        UpdateSchoolProfileCommand command, ICommandHandler<UpdateSchoolProfileCommand, SchoolProfileDto> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();

    private static Task<Results<Ok<SchoolProfileDto>, ProblemHttpResult>> SetLogo(
        IFormFile? file, ICommandHandler<SetSchoolImageCommand, SchoolProfileDto> handler, CancellationToken ct) =>
        SetImageAsync(SchoolImage.Logo, file, handler, ct);

    private static Task<Results<Ok<SchoolProfileDto>, ProblemHttpResult>> SetSignature(
        IFormFile? file, ICommandHandler<SetSchoolImageCommand, SchoolProfileDto> handler, CancellationToken ct) =>
        SetImageAsync(SchoolImage.Signature, file, handler, ct);

    private static async Task<Results<Ok<SchoolProfileDto>, ProblemHttpResult>> SetImageAsync(
        SchoolImage image, IFormFile? file, ICommandHandler<SetSchoolImageCommand, SchoolProfileDto> handler, CancellationToken ct) =>
        await PhotoEndpoints.ReadAsync(file, ct) is { } bytes
            ? (await handler.Handle(new SetSchoolImageCommand(image, bytes), ct)).ToHttpResult()
            : PhotoErrors.Invalid.ToProblem();

    private static async Task<Results<Ok<SchoolProfileDto>, ProblemHttpResult>> RemoveLogo(
        ICommandHandler<RemoveSchoolImageCommand, SchoolProfileDto> handler, CancellationToken ct) =>
        (await handler.Handle(new RemoveSchoolImageCommand(SchoolImage.Logo), ct)).ToHttpResult();

    private static async Task<Results<Ok<SchoolProfileDto>, ProblemHttpResult>> RemoveSignature(
        ICommandHandler<RemoveSchoolImageCommand, SchoolProfileDto> handler, CancellationToken ct) =>
        (await handler.Handle(new RemoveSchoolImageCommand(SchoolImage.Signature), ct)).ToHttpResult();

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> GetLogo(
        IQueryHandler<GetSchoolImageQuery, PhotoContent> handler, CancellationToken ct) =>
        PhotoEndpoints.ToFile(await handler.Handle(new GetSchoolImageQuery(SchoolImage.Logo), ct));

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> GetSignature(
        IQueryHandler<GetSchoolImageQuery, PhotoContent> handler, CancellationToken ct) =>
        PhotoEndpoints.ToFile(await handler.Handle(new GetSchoolImageQuery(SchoolImage.Signature), ct));
}
