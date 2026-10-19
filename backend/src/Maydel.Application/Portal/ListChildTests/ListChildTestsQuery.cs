using Maydel.Application.Cbt;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Portal.ListChildTests;

/// <summary>A child's released online test results (best attempt), newest first.</summary>
public sealed record ListChildTestsQuery(Guid StudentId) : IQuery<IReadOnlyList<ChildTestResult>>;
