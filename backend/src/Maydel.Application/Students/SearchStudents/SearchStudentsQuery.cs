using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;

namespace Maydel.Application.Students.SearchStudents;

/// <summary>
/// Student list for staff (E3.4). <c>search</c> matches every word against first/middle/last name or admission number,
/// so "ada eze" and "MSS/2026/00" both work. Filters combine with AND. Ordered by class, then surname.
/// </summary>
public sealed record SearchStudentsQuery(
    string? Search = null, Guid? ClassRoomId = null, StudentStatus? Status = null, int? Page = null, int? PageSize = null)
    : IQuery<PagedResult<StudentListItem>>;
