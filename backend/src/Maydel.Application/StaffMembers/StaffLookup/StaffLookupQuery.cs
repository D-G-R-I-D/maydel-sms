using Maydel.Application.Common.Messaging;

namespace Maydel.Application.StaffMembers.StaffLookup;

/// <summary>Active staff for pickers (form teacher, subject teacher). Readable by all staff.</summary>
public sealed record StaffLookupQuery : IQuery<IReadOnlyList<StaffLookupItem>>;
