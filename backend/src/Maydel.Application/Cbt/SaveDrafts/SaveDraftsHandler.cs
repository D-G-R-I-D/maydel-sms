using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.SaveDrafts;

internal sealed class SaveDraftsHandler(IAppDbContext db, AccessRules access, ICurrentUser me) : ICommandHandler<SaveDraftsCommand, IReadOnlyList<QuestionDto>>
{
    public async Task<Result<IReadOnlyList<QuestionDto>>> Handle(SaveDraftsCommand c, CancellationToken ct)
    {
        var generation = await db.AiGenerations.AsNoTracking().FirstOrDefaultAsync(g => g.Id == c.GenerationId && g.UserId == me.UserId, ct);
        if (generation is null) return CbtErrors.GenerationNotFound;
        if (!await access.CanAuthorAsync(generation.ClassLevelId, generation.SubjectId, ct)) return CbtErrors.NotYourSubject;
        var drafts = c.Drafts.Questions ?? [];
        if (drafts.Count is 0 or > AiGeneration.MaxQuestions) return Error.Validation("questions", $"Choose between 1 and {AiGeneration.MaxQuestions} questions to save.");

        var saved = new List<Question>();
        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < drafts.Count; i++)
        {
            var d = drafts[i];
            var question = new Question { Text = "", CreatedByUserId = me.UserId, Source = QuestionSource.Ai };
            question.Apply(new QuestionInput(generation.SubjectId, generation.ClassLevelId, d.Kind, d.Text, d.Options, d.TrueFalseAnswer, d.AcceptedAnswers,
                d.MarkingGuide, d.Topic, d.Difficulty));
            if (!Enum.IsDefined(d.Kind) || !Enum.IsDefined(d.Difficulty)) errors[$"questions[{i}]"] = ["Choose the kind and difficulty."];
            else if (question.Normalise() is { } invalid) errors[$"questions[{i}].{invalid.Field ?? "text"}"] = [invalid.Message];
            else saved.Add(question);
        }
        if (errors.Count > 0) return new ValidationError(errors);

        db.Questions.AddRange(saved);
        await db.SaveChangesAsync(ct);
        var ids = saved.Select(q => q.Id).ToList();
        var dtos = await db.QuestionDtos(db.Questions.AsNoTracking().Where(q => ids.Contains(q.Id))).ToListAsync(ct);
        return dtos.OrderBy(d => ids.IndexOf(d.Id)).ToList();
    }
}
