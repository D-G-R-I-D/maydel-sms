using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.GetStatement;

/// <summary>A student's account across all terms: invoices, payments and reversals with a running balance (E10.4).</summary>
public sealed record GetStatementQuery(Guid StudentId) : IQuery<StudentStatement>;
