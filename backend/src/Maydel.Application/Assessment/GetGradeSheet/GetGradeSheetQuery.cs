using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Assessment.GetGradeSheet;

/// <summary>Every active student in the class with their scores for one subject and term (E5.1).</summary>
public sealed record GetGradeSheetQuery(Guid ClassRoomId, Guid SubjectId, Guid TermId) : IQuery<GradeSheet>;
