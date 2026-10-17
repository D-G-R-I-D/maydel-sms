using Maydel.Api.Common;
using Maydel.Application.Admissions;
using Maydel.Application.Admissions.GetAdmissionSettings;
using Maydel.Application.Admissions.GetAdmissionsInfo;
using Maydel.Application.Admissions.GetFormPayment;
using Maydel.Application.Admissions.IssueFormCodes;
using Maydel.Application.Admissions.ListApplications;
using Maydel.Application.Admissions.ListFormCodes;
using Maydel.Application.Admissions.ListMyApplications;
using Maydel.Application.Admissions.RedeemFormCode;
using Maydel.Application.Admissions.Register;
using Maydel.Application.Admissions.SaveAdmissionSettings;
using Maydel.Application.Admissions.StartApplication;
using Maydel.Application.Admissions.StartFormPayment;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>
/// Admissions, part 1 (E11.4): the public "Buy admission form" page, the family's applicant account and applications,
/// paying for the form (online or with a school-issued code), and the staff side (settings, codes, list).
/// </summary>
public static class AdmissionEndpoints
{
    public static void MapAdmissionEndpoints(this IEndpointRouteBuilder app)
    {
        var open = app.MapGroup("/api/public/admissions").WithTags("Admissions").AllowAnonymous();
        var family = app.MapGroup("/api/admissions").WithTags("Admissions").RequireAuthorization(Policies.Applicants);
        var finance = app.MapGroup("/api/admissions").WithTags("Admissions").RequireAuthorization(Policies.Finance);
        var admin = app.MapGroup("/api/admissions").WithTags("Admissions").RequireAuthorization(Policies.Admin);

        open.MapGet("", Info).WithName("GetAdmissionsInfo");
        // Sign-ups create accounts: rate-limited per IP like the other public forms.
        open.MapPost("/register", Register).WithName("RegisterApplicant").RequireRateLimiting(RateLimits.PublicForms).ProducesProblems(400, 404, 409, 422, 429);

        family.MapGet("/my-applications", Mine).WithName("ListMyApplications").ProducesProblems(401, 403);
        family.MapPost("/applications", Start).WithName("StartApplication").ProducesProblems(400, 401, 403, 404, 422);
        family.MapPost("/applications/{id:guid}/redeem-code", Redeem).WithName("RedeemFormCode").ProducesProblems(400, 401, 403, 404, 422);
        family.MapPost("/applications/{id:guid}/pay", Pay).WithName("StartFormPayment").ProducesProblems(401, 403, 404, 422);
        family.MapGet("/payments/{reference}", Payment).WithName("GetFormPayment").ProducesProblems(401, 403, 404);

        finance.MapGet("/settings", GetSettings).WithName("GetAdmissionSettings").ProducesProblems(400, 401, 403, 404);
        admin.MapPut("/settings", SaveSettings).WithName("SaveAdmissionSettings").ProducesProblems(400, 401, 403, 404);
        finance.MapPost("/form-codes", IssueCodes).WithName("IssueFormCodes").ProducesProblems(400, 401, 403, 404);
        finance.MapGet("/form-codes", Codes).WithName("ListFormCodes").ProducesProblems(400, 401, 403);
        finance.MapGet("/applications", List).WithName("ListApplications").ProducesProblems(400, 401, 403);
    }

    private static async Task<Results<Ok<IReadOnlyList<AdmissionsInfo>>, ProblemHttpResult>> Info(IQueryHandler<GetAdmissionsInfoQuery, IReadOnlyList<AdmissionsInfo>> h, CancellationToken ct) =>
        (await h.Handle(new GetAdmissionsInfoQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicationDto>, ProblemHttpResult>> Register(RegisterApplicantCommand command, ICommandHandler<RegisterApplicantCommand, ApplicationDto> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ApplicationDto>>, ProblemHttpResult>> Mine(IQueryHandler<ListMyApplicationsQuery, IReadOnlyList<ApplicationDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListMyApplicationsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicationDto>, ProblemHttpResult>> Start(ApplyChild body, ICommandHandler<StartApplicationCommand, ApplicationDto> h, CancellationToken ct) =>
        (await h.Handle(new StartApplicationCommand(body), ct)).ToHttpResult();

    public sealed record CodeRequest(string Code);

    private static async Task<Results<Ok<ApplicationDto>, ProblemHttpResult>> Redeem(Guid id, CodeRequest body, ICommandHandler<RedeemFormCodeCommand, ApplicationDto> h, CancellationToken ct) =>
        (await h.Handle(new RedeemFormCodeCommand(id, body.Code), ct)).ToHttpResult();

    private static async Task<Results<Ok<OnlineCheckout>, ProblemHttpResult>> Pay(Guid id, ICommandHandler<StartFormPaymentCommand, OnlineCheckout> h, CancellationToken ct) =>
        (await h.Handle(new StartFormPaymentCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<FormPayments.Outcome>, ProblemHttpResult>> Payment(string reference, IQueryHandler<GetFormPaymentQuery, FormPayments.Outcome> h, CancellationToken ct) =>
        (await h.Handle(new GetFormPaymentQuery(reference), ct)).ToHttpResult();

    private static async Task<Results<Ok<AdmissionSettingsDto>, ProblemHttpResult>> GetSettings([AsParameters] GetAdmissionSettingsQuery query, IQueryHandler<GetAdmissionSettingsQuery, AdmissionSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<AdmissionSettingsDto>, ProblemHttpResult>> SaveSettings(SaveAdmissionSettingsCommand command, ICommandHandler<SaveAdmissionSettingsCommand, AdmissionSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<FormCodeDto>>, ProblemHttpResult>> IssueCodes(IssueFormCodesCommand command, ICommandHandler<IssueFormCodesCommand, IReadOnlyList<FormCodeDto>> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<FormCodeDto>>, ProblemHttpResult>> Codes([AsParameters] ListFormCodesQuery query, IQueryHandler<ListFormCodesQuery, PagedResult<FormCodeDto>> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<ApplicationDto>>, ProblemHttpResult>> List([AsParameters] ListApplicationsQuery query, IQueryHandler<ListApplicationsQuery, PagedResult<ApplicationDto>> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();
}
