using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.GetApplication;

/// <summary>An application with its form and documents. Families: their own only (404 otherwise); staff: any.</summary>
public sealed record GetApplicationQuery(Guid Id, bool AsStaff = false) : IQuery<ApplicationDetail>;
