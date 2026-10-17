using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;

namespace Maydel.Application.Admissions.UploadApplicationDocument;

/// <summary>Attaches a photo (re-encoded) or PDF to the application while it's still editable. One passport photo: a new one replaces it.</summary>
public sealed record UploadApplicationDocumentCommand(Guid ApplicationId, DocumentKind Kind, byte[] File) : ICommand<ApplicationDetail>;
