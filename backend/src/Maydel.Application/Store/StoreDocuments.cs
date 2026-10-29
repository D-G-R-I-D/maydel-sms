using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;
using Maydel.Application.SchoolSettings;
using Maydel.Domain.People;
using Maydel.Domain.Store;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Store;

/// <summary>A shop order's receipt PDF: staff, or the child's parents for their own orders.</summary>
public sealed record GetStoreReceiptQuery(Guid OrderId, Guid? StudentId = null) : IQuery<PdfFile>;

/// <summary>One student's ID card (admin).</summary>
public sealed record GetIdCardQuery(Guid StudentId) : IQuery<PdfFile>;

/// <summary>ID cards for every active student in a class, ten to an A4 sheet (admin).</summary>
public sealed record GetClassIdCardsQuery(Guid ClassRoomId) : IQuery<PdfFile>;

internal sealed class GetStoreReceiptHandler(IAppDbContext db, AccessRules access, SchoolBranding branding, IStoreDocumentRenderer renderer)
    : IQueryHandler<GetStoreReceiptQuery, PdfFile>
{
    public async Task<Result<PdfFile>> Handle(GetStoreReceiptQuery q, CancellationToken ct)
    {
        var order = await db.StoreOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == q.OrderId, ct);
        // From the portal: only this family's child's order.
        if (order is null || (q.StudentId is { } sid && (order.StudentId != sid || !await access.PortalStudents().AnyAsync(s => s.Id == sid, ct))))
            return StoreErrors.OrderNotFound;
        var dto = (await db.DtosAsync([order], ct))[0];
        var room = await db.Students.AsNoTracking().Where(s => s.Id == order.StudentId && s.ClassRoom != null)
            .Select(s => s.ClassRoom!.ClassLevel.Name + " " + s.ClassRoom.Arm).FirstOrDefaultAsync(ct);
        var status = order.Status == OrderStatus.Cancelled ? "Cancelled" : order.Payment switch
        {
            OrderPayment.Paid => "Paid",
            OrderPayment.ChargedToFees => "Added to the fees account",
            _ => "Not paid yet",
        };
        var method = order.PaidWith switch
        {
            StorePayMethod.BankTransfer => "Bank transfer",
            StorePayMethod.Pos => "POS",
            StorePayMethod.Fees => "Fees account",
            StorePayMethod.Cash => "Cash",
            _ => null,
        };
        var pdf = renderer.Receipt(new StoreReceiptDocument(order.Number, order.PaidAt ?? order.CreatedAt, dto.Student, dto.AdmissionNumber, room,
            [.. order.Lines.Select(l => new StoreReceiptLine(l.ItemName, l.VariantLabel, l.Quantity, l.UnitPrice, l.LineTotal))], order.Total, status, method,
            order.PaymentReference, dto.InvoiceNumber, order.Payment != OrderPayment.Unpaid, order.Status == OrderStatus.Cancelled), await branding.LoadAsync(ct));
        return new PdfFile(pdf, $"Receipt {order.Number}.pdf");
    }
}

internal sealed class IdCards(IAppDbContext db, SchoolBranding branding, IStoreDocumentRenderer renderer, ISchoolClock clock)
{
    public async Task<Result<PdfFile>> RenderAsync(IQueryable<Student> students, bool sheet, string fileName, CancellationToken ct)
    {
        var session = await db.Sessions.AsNoTracking().Where(s => s.IsCurrent).Select(s => new { s.Name, s.EndsOn }).FirstOrDefaultAsync(ct)
            ?? await db.Sessions.AsNoTracking().Where(s => s.StartsOn <= clock.Today && s.EndsOn >= clock.Today).Select(s => new { s.Name, s.EndsOn }).FirstOrDefaultAsync(ct);
        if (session is null) return Error.Rule("idcard.no_session", "Set the current session first: the card shows it and is valid until it ends.");
        var rows = await students.OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.FirstName, s.LastName, s.AdmissionNumber, s.PhotoPath, Room = s.ClassRoom == null ? null : s.ClassRoom.ClassLevel.Name + " " + s.ClassRoom.Arm })
            .ToListAsync(ct);
        if (rows.Count == 0) return Error.NotFound("idcard.none", "There are no active students to print cards for.");
        var cards = new List<IdCardDocument>();
        foreach (var s in rows)
            cards.Add(new IdCardDocument($"{s.FirstName} {s.LastName}", s.AdmissionNumber, s.Room, session.Name, session.EndsOn, await branding.ReadAsync(s.PhotoPath, ct)));
        return new PdfFile(renderer.IdCards(cards, await branding.LoadAsync(ct), sheet), fileName);
    }
}

internal sealed class GetIdCardHandler(IAppDbContext db, IdCards cards) : IQueryHandler<GetIdCardQuery, PdfFile>
{
    public async Task<Result<PdfFile>> Handle(GetIdCardQuery q, CancellationToken ct)
    {
        var student = db.Students.AsNoTracking().Where(s => s.Id == q.StudentId && s.Status == StudentStatus.Active);
        var number = await student.Select(s => s.AdmissionNumber).FirstOrDefaultAsync(ct);
        if (number is null) return Error.NotFound("student.not_found", "That student doesn't exist or isn't active.");
        return await cards.RenderAsync(student, sheet: false, $"ID card {number.Replace('/', '-')}.pdf", ct);
    }
}

internal sealed class GetClassIdCardsHandler(IAppDbContext db, IdCards cards) : IQueryHandler<GetClassIdCardsQuery, PdfFile>
{
    public async Task<Result<PdfFile>> Handle(GetClassIdCardsQuery q, CancellationToken ct)
    {
        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == q.ClassRoomId).Select(r => r.ClassLevel.Name + " " + r.Arm).FirstOrDefaultAsync(ct);
        if (room is null) return Error.NotFound("class.not_found", "That class doesn't exist.");
        return await cards.RenderAsync(db.Students.AsNoTracking().Where(s => s.ClassRoomId == q.ClassRoomId && s.Status == StudentStatus.Active),
            sheet: true, $"ID cards {room}.pdf", ct);
    }
}
