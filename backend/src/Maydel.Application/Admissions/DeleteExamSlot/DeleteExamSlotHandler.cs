using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.DeleteExamSlot;

internal sealed class DeleteExamSlotHandler(IAppDbContext db) : ICommandHandler<DeleteExamSlotCommand>
{
    public async Task<Result> Handle(DeleteExamSlotCommand c, CancellationToken ct)
    {
        var slot = await db.ExamSlots.FirstOrDefaultAsync(s => s.Id == c.Id, ct);
        if (slot is null) return AdmissionErrors.SlotNotFound;
        if (await db.Applications.AnyAsync(a => a.ExamSlotId == c.Id, ct)) return AdmissionErrors.SlotHasBookings;
        db.ExamSlots.Remove(slot);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
