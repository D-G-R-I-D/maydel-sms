using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.IssueFormCodes;

/// <summary>The bursar records form fees paid at the school and gets one-time codes to hand to the families (1–50 at a time).</summary>
public sealed record IssueFormCodesCommand(Guid SessionId, int Count, decimal AmountPaid, string? IssuedTo) : ICommand<IReadOnlyList<FormCodeDto>>;
