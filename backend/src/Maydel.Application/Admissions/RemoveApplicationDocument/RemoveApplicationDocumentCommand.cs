using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.RemoveApplicationDocument;

public sealed record RemoveApplicationDocumentCommand(Guid ApplicationId, Guid DocumentId) : ICommand<ApplicationDetail>;
