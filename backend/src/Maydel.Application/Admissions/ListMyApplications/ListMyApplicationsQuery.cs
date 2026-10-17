using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.ListMyApplications;

public sealed record ListMyApplicationsQuery : IQuery<IReadOnlyList<ApplicationDto>>;
