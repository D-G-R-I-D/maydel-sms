using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;

namespace Maydel.Application.Finance.SaveFeeSettings;

/// <summary>Sets the sibling discount rule (admin). Applies to invoices generated afterwards; existing invoices don't change.</summary>
public sealed record SaveFeeSettingsCommand(SiblingDiscountMode SiblingMode, decimal SecondChild, decimal ThirdAndLater) : ICommand<FeeSettingsDto>;
