using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.SubmitApplication;

/// <summary>Submits the form (E11.5). It's then locked; the family gets a confirmation email with the exam deadline.</summary>
public sealed record SubmitApplicationCommand(Guid ApplicationId) : ICommand<ApplicationDetail>;
