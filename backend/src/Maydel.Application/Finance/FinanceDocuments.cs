using Maydel.Application.Abstractions;
using Maydel.Application.Reports;
using Maydel.Application.SchoolSettings;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance;

/// <summary>Builds invoice and receipt PDFs. Callers check access first.</summary>
public sealed class FinanceDocuments(IAppDbContext db, ISchoolClock clock, SchoolBranding branding, IFinanceDocumentRenderer renderer)
{
    public async Task<PdfFile?> InvoiceAsync(Guid invoiceId, CancellationToken ct)
    {
        if (await db.DetailAsync(invoiceId, clock.Today, ct) is not { } detail) return null;
        var guardian = await db.StudentGuardians.AsNoTracking().Where(g => g.StudentId == detail.StudentId)
            .OrderByDescending(g => g.IsPrimaryContact).Select(g => g.Guardian.FullName).FirstOrDefaultAsync(ct);
        var bytes = renderer.Invoice(new InvoiceDocument(detail, guardian, clock.Today), await branding.LoadAsync(ct));
        return new PdfFile(bytes, ReportPdfs.Safe($"{detail.Number} - {detail.StudentName}") + ".pdf");
    }

    public async Task<PdfFile?> ReceiptAsync(Guid paymentId, CancellationToken ct)
    {
        var invoiceId = await db.Payments.Where(p => p.Id == paymentId).Select(p => (Guid?)p.InvoiceId).FirstOrDefaultAsync(ct);
        if (invoiceId is null) return null;
        var invoice = await db.Invoices.AsNoTracking().Include(i => i.Payments).Include(i => i.Student).Include(i => i.Term).ThenInclude(t => t.Session)
            .FirstAsync(i => i.Id == invoiceId, ct);
        var payment = invoice.Payments.Single(p => p.Id == paymentId);
        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == invoice.ClassRoomId).Select(r => r.ClassLevel.Name + " " + r.Arm).FirstOrDefaultAsync(ct);

        // As at this payment: later payments and reversals don't change a reprint.
        var paidToDate = invoice.Payments.Where(p => p.CreatedAt < payment.CreatedAt || p.Id == payment.Id).Sum(p => p.Amount);
        var receipt = Invoices.Receipt(payment, invoice, room) with { BalanceAfter = invoice.Total - paidToDate };
        var reverses = payment.ReversesPaymentId is { } r ? invoice.Payments.FirstOrDefault(p => p.Id == r)?.ReceiptNumber : null;
        var bytes = renderer.Receipt(new ReceiptDocument(receipt, invoice.Total, paidToDate, payment.Note, reverses), await branding.LoadAsync(ct));
        return new PdfFile(bytes, ReportPdfs.Safe($"{payment.ReceiptNumber} - {receipt.StudentName}") + ".pdf");
    }
}
