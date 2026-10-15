using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.ListDebtors;

/// <summary>Students who still owe for a term, largest balance first, with what's overdue today (E10.4).</summary>
public sealed record ListDebtorsQuery(Guid TermId, Guid? ClassRoomId = null, bool OverdueOnly = false) : IQuery<DebtorsReport>;
