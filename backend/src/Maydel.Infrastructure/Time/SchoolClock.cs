using Maydel.Application.Abstractions;
using Maydel.Domain.Attendance;

namespace Maydel.Infrastructure.Time;

internal sealed class SchoolClock(TimeProvider time) : ISchoolClock
{
    public DateOnly Today => RegisterRules.TodayAt(time.GetUtcNow());
    public DateTimeOffset Now => time.GetUtcNow();
}
