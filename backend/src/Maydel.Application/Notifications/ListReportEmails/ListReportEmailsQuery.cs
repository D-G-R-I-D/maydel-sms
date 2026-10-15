using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Notifications.ListReportEmails;

/// <summary>Report emails for a class and term, newest first, with delivery status. Admins only.</summary>
public sealed record ListReportEmailsQuery(Guid ClassRoomId, Guid TermId) : IQuery<IReadOnlyList<ReportEmailStatus>>;
