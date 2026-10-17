using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;

namespace Maydel.Application.Admissions.GetApplicationFormPdf;

/// <summary>
/// The admission form as a PDF (E11.5): blank for a session (<c>ApplicationId</c> null, to print and fill by hand), or an
/// application's form as filled so far (the family's own, or any for staff).
/// </summary>
public sealed record GetApplicationFormPdfQuery(Guid? ApplicationId, Guid? SessionId, bool AsStaff = false) : IQuery<PdfFile>;
