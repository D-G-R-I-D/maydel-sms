using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Portal.SubmitPaymentProof;

internal sealed class SubmitPaymentProofHandler(IAppDbContext db, AccessRules access, PaymentProofs proofs, ISchoolClock clock, ICurrentUser me)
    : ICommandHandler<SubmitPaymentProofCommand, ProofDto>
{
    public async Task<Result<ProofDto>> Handle(SubmitPaymentProofCommand c, CancellationToken ct)
    {
        if (!await access.PortalStudents().AnyAsync(s => s.Id == c.StudentId, ct)) return PortalErrors.StudentNotFound;
        var invoice = await db.Invoices.Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == c.InvoiceId && i.StudentId == c.StudentId && !i.IsCancelled && i.PublishedAt != null, ct);
        if (invoice is null) return PortalErrors.InvoiceNotFound;
        if (invoice.Balance <= 0) return FinanceErrors.NothingOwed;
        if (c.Amount > invoice.Balance) return Error.Validation("amount", $"That's more than the outstanding balance of ₦{invoice.Balance:N2}.");
        if (c.PaidOn > clock.Today) return Error.Validation("paidOn", "The payment date can't be in the future.");
        if (await db.PaymentProofs.CountAsync(p => p.InvoiceId == invoice.Id && p.Status == ProofStatus.Pending, ct) >= PaymentProof.MaxPendingPerInvoice)
            return FinanceErrors.TooManyProofs;

        if (await proofs.SaveAsync(c.File, ct) is not { } file) return FinanceErrors.ProofFile;
        var proof = new PaymentProof
        {
            InvoiceId = invoice.Id, StudentId = c.StudentId, Amount = c.Amount, PaidOn = c.PaidOn,
            BankReference = string.IsNullOrWhiteSpace(c.BankReference) ? null : c.BankReference.Trim(),
            Note = string.IsNullOrWhiteSpace(c.Note) ? null : c.Note.Trim(),
            FilePath = file.Key, ContentType = file.ContentType, SubmittedByUserId = me.UserId,
        };
        db.PaymentProofs.Add(proof);
        await db.SaveChangesAsync(ct);
        return await proofs.Dtos(db.PaymentProofs.Where(p => p.Id == proof.Id)).FirstAsync(ct);
    }
}
