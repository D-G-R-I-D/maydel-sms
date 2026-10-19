using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.SaveAiSettings;

/// <summary>Admin: switch AI question writing on/off and set the default allowance per teacher and the school-wide monthly cap.</summary>
public sealed record SaveAiSettingsCommand(bool Enabled, int DefaultMonthlyGenerations, int SchoolMonthlyGenerations) : ICommand<AiSettingsDto>;

/// <summary>Admin: give one teacher their own monthly allowance, or null to go back to the default.</summary>
public sealed record SetAiAllowanceCommand(Guid UserId, int? MonthlyGenerations) : ICommand<AiUsageRow>;
