using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.PublishTest;

/// <summary>Publishes a ready draft (locks it; the classes see it in its window) or, with <c>Publish</c> false, takes it back to draft.</summary>
public sealed record PublishTestCommand(Guid Id, bool Publish = true) : ICommand<TestDetail>;
