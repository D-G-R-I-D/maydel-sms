using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.SaveApplicationForm;

/// <summary>Saves the form as a draft (E11.5): after paying, before submitting. Blank fields are fine until submission.</summary>
public sealed record SaveApplicationFormCommand(Guid ApplicationId, SaveFormRequest Body) : ICommand<ApplicationDetail>;
