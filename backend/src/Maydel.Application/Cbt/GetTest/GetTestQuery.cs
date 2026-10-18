using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.GetTest;

public sealed record GetTestQuery(Guid Id) : IQuery<TestDetail>;
