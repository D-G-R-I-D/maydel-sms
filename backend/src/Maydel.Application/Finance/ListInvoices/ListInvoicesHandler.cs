using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.ListInvoices;

internal sealed class ListInvoicesHandler(IAppDbContext db) : IQueryHandler<ListInvoicesQuery, PagedResult<InvoiceSummary>>
{
    public async Task<Result<PagedResult<InvoiceSummary>>> Handle(ListInvoicesQuery q, CancellationToken ct)
    {
        var invoices = db.Invoices.AsNoTracking();
        if (q.TermId is { } term) invoices = invoices.Where(i => i.TermId == term);
        if (q.ClassRoomId is { } room) invoices = invoices.Where(i => i.ClassRoomId == room);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            invoices = invoices.Where(i => i.Number.ToLower().Contains(s) || i.Student.AdmissionNumber.ToLower().Contains(s)
                || (i.Student.FirstName + " " + i.Student.LastName).ToLower().Contains(s));
        }

        var rows = invoices.Select(i => new
        {
            i.Id, i.Number, i.StudentId, i.Student.AdmissionNumber, Name = i.Student.FirstName + " " + i.Student.LastName,
            Room = db.ClassRooms.Where(r => r.Id == i.ClassRoomId).Select(r => r.ClassLevel.Name + " " + r.Arm).FirstOrDefault(),
            i.TermId, Term = i.Term.Number.ToString() + " Term " + i.Term.Session.Name,
            Total = i.Lines.Sum(l => l.Amount), Paid = i.Payments.Sum(p => (decimal?)p.Amount) ?? 0, i.IsCancelled, i.DueOn, i.CreatedAt,
        });
        rows = q.Status switch
        {
            InvoiceStatus.Cancelled => rows.Where(r => r.IsCancelled),
            InvoiceStatus.Unpaid => rows.Where(r => !r.IsCancelled && r.Paid <= 0),
            InvoiceStatus.PartiallyPaid => rows.Where(r => !r.IsCancelled && r.Paid > 0 && r.Paid < r.Total),
            InvoiceStatus.Paid => rows.Where(r => !r.IsCancelled && r.Paid >= r.Total),
            _ => rows,
        };
        var page = await rows.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Number).ToPagedAsync(q.Page, q.PageSize, ct);
        return new PagedResult<InvoiceSummary>([.. page.Items.Select(r => new InvoiceSummary(
            r.Id, r.Number, r.StudentId, r.AdmissionNumber, r.Name, r.Room, r.TermId, r.Term, r.Total, r.Paid, r.Total - r.Paid,
            Invoices.StatusOf(r.IsCancelled, r.Total, r.Paid), r.DueOn, r.CreatedAt))], page.Page, page.PageSize, page.TotalCount);
    }
}
