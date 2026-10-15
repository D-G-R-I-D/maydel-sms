using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Portal.GetChildFees;

/// <summary>The parent's child's (or the student's own) invoices, payments and balance. Cancelled invoices are hidden.</summary>
public sealed record GetChildFeesQuery(Guid StudentId) : IQuery<PortalFees>;
