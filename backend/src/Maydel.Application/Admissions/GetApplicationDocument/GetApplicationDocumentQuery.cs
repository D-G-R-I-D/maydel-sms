using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;

namespace Maydel.Application.Admissions.GetApplicationDocument;

/// <summary>A document file: the family's own application, or any for staff. Always served as a download.</summary>
public sealed record GetApplicationDocumentQuery(Guid ApplicationId, Guid DocumentId, bool AsStaff = false) : IQuery<PhotoContent>;
