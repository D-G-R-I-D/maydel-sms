using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Common.Access;

namespace Maydel.Application.Assessment.GetGradeSheet;

internal sealed class GetGradeSheetHandler(IAppDbContext db, AccessRules access) : IQueryHandler<GetGradeSheetQuery, GradeSheet>
{
    public async Task<Result<GradeSheet>> Handle(GetGradeSheetQuery q, CancellationToken ct)
    {
        if (!await access.CanGradeAsync(q.ClassRoomId, q.SubjectId, ct)) return AssessmentErrors.NotGrader;
        return await GradeSheetReader.ReadAsync(db, q.ClassRoomId, q.SubjectId, q.TermId, ct);
    }
}
