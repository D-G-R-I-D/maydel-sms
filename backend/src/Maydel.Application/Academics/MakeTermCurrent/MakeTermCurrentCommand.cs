using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.MakeTermCurrent;

/// <summary>Makes this term (and its session) current. The previous current term and session are unset in the same transaction (E2.1).</summary>
public sealed record MakeTermCurrentCommand(Guid Id) : ICommand<TermDto>;
