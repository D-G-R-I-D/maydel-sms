using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Students.SearchStudents;

internal sealed class SearchStudentsHandler(IAppDbContext db) : IQueryHandler<SearchStudentsQuery, PagedResult<StudentListItem>>
{
    public async Task<Result<PagedResult<StudentListItem>>> Handle(SearchStudentsQuery q, CancellationToken ct)
    {
        var students = db.Students.AsNoTracking();
        if (q.ClassRoomId is { } classRoomId) students = students.Where(s => s.ClassRoomId == classRoomId);
        if (q.Status is { } status) students = students.Where(s => s.Status == status);
        foreach (var word in (q.Search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5))
        {
            var w = word.ToLower();
            students = students.Where(s => s.FirstName.ToLower().Contains(w) || s.LastName.ToLower().Contains(w)
                || (s.MiddleName != null && s.MiddleName.ToLower().Contains(w)) || s.AdmissionNumber.ToLower().Contains(w));
        }

        return await students
            .OrderBy(s => s.ClassRoom == null ? int.MaxValue : s.ClassRoom.ClassLevel.Order).ThenBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new StudentListItem(
                s.Id, s.AdmissionNumber, s.FirstName + " " + s.LastName, s.FirstName, s.LastName, s.Sex, s.ClassRoomId,
                s.ClassRoom != null ? s.ClassRoom.ClassLevel.Name + " " + s.ClassRoom.Arm : null, s.Status, s.UserId != null))
            .ToPagedAsync(q.Page, q.PageSize, ct);
    }
}
