using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.ListFormCodes;

public sealed record ListFormCodesQuery(Guid SessionId, bool? Used = null, int? Page = null, int? PageSize = null) : IQuery<PagedResult<FormCodeDto>>;
