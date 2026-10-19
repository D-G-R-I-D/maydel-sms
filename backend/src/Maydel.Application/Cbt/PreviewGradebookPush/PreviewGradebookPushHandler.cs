using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.PreviewGradebookPush;

internal sealed class PreviewGradebookPushHandler(Tests tests, Gradebook gradebook) : IQueryHandler<PreviewGradebookPushQuery, GradebookPreview>
{
    public async Task<Result<GradebookPreview>> Handle(PreviewGradebookPushQuery q, CancellationToken ct)
    {
        if (await tests.ManageableAsync(q.TestId, ct, track: false) is not { } test) return CbtErrors.TestNotFound;
        if (test.ResultsReleasedAt is null) return CbtErrors.NotReleased;
        return await gradebook.PreviewAsync(test, ct) is { } preview ? preview.Preview : CbtErrors.NoComponent;
    }
}
