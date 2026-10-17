using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.SaveApplicationForm;

/// <summary>
/// Family: saves the form as a draft (E11.5), after paying and before submitting; blank fields are fine until submission.
/// Admin (<c>AsAdmin</c>): corrects any submitted application until enrolment; the form must stay complete.
/// </summary>
public sealed record SaveApplicationFormCommand(Guid ApplicationId, SaveFormRequest Body, bool AsAdmin = false) : ICommand<ApplicationDetail>;
