using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;

namespace Maydel.Application.Admissions.GetAdmissionLetter;

/// <summary>The admission letter PDF: the family's own offer, or any for staff. 404 unless the offer stands.</summary>
public sealed record GetAdmissionLetterQuery(Guid ApplicationId, bool AsStaff = false) : IQuery<PdfFile>;
