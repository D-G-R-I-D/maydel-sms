using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Notifications.RetryEmail;

/// <summary>Queues a failed email again (e.g. after fixing the address or the provider). Admins only.</summary>
public sealed record RetryEmailCommand(Guid EmailId) : ICommand;
