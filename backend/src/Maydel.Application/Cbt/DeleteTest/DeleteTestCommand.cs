using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.DeleteTest;

/// <summary>Deletes a draft test (published ones must be unpublished first).</summary>
public sealed record DeleteTestCommand(Guid Id) : ICommand;
