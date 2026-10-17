using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.StartApplication;

/// <summary>A signed-in family (applicant or existing parent) applies for another child.</summary>
public sealed record StartApplicationCommand(ApplyChild Child) : ICommand<ApplicationDto>;
