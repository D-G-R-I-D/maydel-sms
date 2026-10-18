using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.SaveTestSections;

/// <summary>Replaces a draft test's sections (order as sent). Fixed questions must come from the test's subject and level bank.</summary>
public sealed record SaveTestSectionsCommand(Guid TestId, IReadOnlyList<SectionInput> Sections) : ICommand<TestDetail>;
