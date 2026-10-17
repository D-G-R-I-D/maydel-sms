using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;

namespace Maydel.Application.Finance.GetPaymentProofFile;

public sealed record GetPaymentProofFileQuery(Guid ProofId) : IQuery<PhotoContent>;
