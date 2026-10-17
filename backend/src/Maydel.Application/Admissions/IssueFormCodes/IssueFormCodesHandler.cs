using System.Security.Cryptography;
using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.IssueFormCodes;

internal sealed class IssueFormCodesHandler(IAppDbContext db, ICurrentUser me) : ICommandHandler<IssueFormCodesCommand, IReadOnlyList<FormCodeDto>>
{
    public async Task<Result<IReadOnlyList<FormCodeDto>>> Handle(IssueFormCodesCommand c, CancellationToken ct)
    {
        if (!await db.Sessions.AnyAsync(s => s.Id == c.SessionId, ct)) return AdmissionErrors.SessionNotFound;
        if (c.Count is < 1 or > 50) return Error.Validation("count", "Issue between 1 and 50 codes at a time.");
        if (c.AmountPaid < 0 || decimal.Round(c.AmountPaid, 2) != c.AmountPaid) return Error.Validation("amountPaid", "Enter the amount paid per form.");

        var codes = new List<FormCode>();
        while (codes.Count < c.Count)
        {
            var code = FormCode.Generate(RandomNumberGenerator.GetInt32);
            if (codes.Any(x => x.Code == code) || await db.FormCodes.AnyAsync(x => x.Code == code, ct)) continue;
            codes.Add(new FormCode
            {
                Code = code, SessionId = c.SessionId, AmountPaid = c.AmountPaid, IssuedByUserId = me.UserId,
                IssuedTo = string.IsNullOrWhiteSpace(c.IssuedTo) ? null : c.IssuedTo.Trim(),
            });
        }
        db.FormCodes.AddRange(codes);
        await db.SaveChangesAsync(ct);
        return codes.Select(x => new FormCodeDto(x.Id, x.Code, x.SessionId, x.AmountPaid, x.IssuedTo, false, null, x.CreatedAt)).ToList();
    }
}
