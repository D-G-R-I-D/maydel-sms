using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;

namespace Maydel.Application.Portal.GetChildProofFile;

public sealed record GetChildProofFileQuery(Guid StudentId, Guid ProofId) : IQuery<PhotoContent>;
