using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.RedeemFormCode;

/// <summary>The family enters the code the bursar gave them after paying at the school; it unlocks the form once.</summary>
public sealed record RedeemFormCodeCommand(Guid ApplicationId, string Code) : ICommand<ApplicationDto>;
