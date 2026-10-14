using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.SetBehaviour;

/// <summary>
/// The behaviour ratings (A–E per trait) and clubs on a student's report (E2.5). Form teacher of the class or an admin.
/// Replaces what was there. Never affects scores, averages or positions. Refused (422) once the report is published.
/// </summary>
public sealed record SetBehaviourCommand(Guid StudentId, Guid TermId, IReadOnlyList<TraitRatingInput> Ratings, IReadOnlyList<ClubLine> Clubs) : ICommand<ReportCard>;
