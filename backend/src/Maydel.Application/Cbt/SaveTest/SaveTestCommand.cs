using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.SaveTest;

/// <summary>Creates a draft test (<c>Id</c> null) or edits a draft's header: subject, level, term, component, classes, timing, attempts, shuffling.</summary>
public sealed record SaveTestCommand(Guid? Id, TestInput Test) : ICommand<TestDetail>;
