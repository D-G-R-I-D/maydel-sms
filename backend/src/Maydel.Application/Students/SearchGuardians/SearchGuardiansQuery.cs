using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Students.SearchGuardians;

/// <summary>Guardian directory. <c>search</c> matches name, email or phone (any format: "0803…" finds "+234803…").</summary>
public sealed record SearchGuardiansQuery(string? Search = null, int? Page = null, int? PageSize = null) : IQuery<PagedResult<GuardianListItem>>;
