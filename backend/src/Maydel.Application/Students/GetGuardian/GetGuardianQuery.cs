using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Students.GetGuardian;

public sealed record GetGuardianQuery(Guid Id) : IQuery<GuardianDetail>;
