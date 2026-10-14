using System.Globalization;
using Maydel.Application.Abstractions;
using Maydel.Application.Reports;
using Maydel.Domain.Assessment;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Maydel.Infrastructure.Reports;

/// <summary>
/// The default report card layout (E5.6), modelled on the school's Excel result sheet: crest and school details,
/// student details, one column per score component, total / grade / remark / comment, class figures, the term
/// comparison, attendance, the grading key, behaviour ratings and clubs, and comments. A4 portrait, one student per page. Drafts carry a watermark.
/// QuestPDF is used under its Community licence (free for organisations under US$1M annual revenue).
/// </summary>
internal sealed class QuestPdfReportCardRenderer : IReportCardRenderer
{
    private static readonly Lazy<byte[]> Logo = new(() =>
    {
        using var stream = typeof(QuestPdfReportCardRenderer).Assembly.GetManifestResourceStream("Maydel.ReportLogo.png")
            ?? throw new InvalidOperationException("Report logo resource missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    });

    private const string Navy = "#234a6d";
    private const string Gold = "#b8924a";
    private const string Fill = "#ebf1f7";

    static QuestPdfReportCardRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(IReadOnlyList<ReportCardDocument> cards, ReportBranding profile)
    {
        return Document.Create(doc =>
        {
            foreach (var card in cards) doc.Page(page => ComposePage(page, card, profile));
        })
        .WithMetadata(new DocumentMetadata { Title = cards.Count == 1 ? $"Report card – {cards[0].Card.StudentName}" : "Report cards", Author = profile.Name })
        .GeneratePdf();
    }

    private static void ComposePage(PageDescriptor page, ReportCardDocument document, ReportBranding school)
    {
        var card = document.Card;
        page.Size(PageSizes.A4);
        page.Margin(22);
        page.DefaultTextStyle(t => t.FontSize(8).FontColor("#1d2733"));

        // Behind the content, in a pale tint, so it never hides results.
        if (card.Status != ReportStatus.Published)
            page.Background().AlignCenter().AlignMiddle().Rotate(-35)
                .Text("DRAFT – NOT FINAL").FontSize(64).Bold().FontColor("#e3e9f0");

        page.Header().Element(h => ComposeHeader(h, card, document.Photo, school));
        page.Content().PaddingTop(6).Column(col =>
        {
            col.Spacing(6);
            col.Item().Element(c => ComposeDetails(c, card));
            col.Item().Element(c => ComposeSubjects(c, card));
            col.Item().Element(c => ComposeSummary(c, card));
            if (card.PreviousTerms.Count > 0) col.Item().Element(c => ComposeComparison(c, card));
            col.Item().Element(c => ComposeGradingKey(c, card));
            if (card.Behaviour.Count > 0 || card.Clubs.Count > 0) col.Item().Element(c => ComposeBehaviour(c, card));
            col.Item().Element(c => ComposeComments(c, card, school));
        });
        page.Footer().Row(row =>
        {
            row.RelativeItem().Text(t =>
            {
                t.Span("Resumption: ").Bold();
                t.Span(card.NextTermBegins is { } next ? next.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture) : "To be announced");
            });
            row.RelativeItem().AlignRight().Text(card.Status == ReportStatus.Published
                ? $"Published {card.PublishedAt:d MMM yyyy}"
                : "Draft preview – figures may change").FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void ComposeHeader(IContainer container, ReportCard card, byte[]? photo, ReportBranding school) =>
        container.BorderBottom(2).BorderColor(Navy).PaddingBottom(6).Row(row =>
        {
            row.ConstantItem(62).Height(62).Image(school.Logo ?? Logo.Value).FitArea();
            row.RelativeItem().PaddingHorizontal(8).AlignMiddle().Column(c =>
            {
                c.Item().AlignCenter().Text(school.Name.ToUpperInvariant()).FontSize(15).Bold().FontColor(Navy);
                if (school.Motto is { } motto) c.Item().AlignCenter().Text(motto).Italic().FontColor(Gold);
                c.Item().AlignCenter().Text(school.Address).FontSize(7);
                c.Item().AlignCenter().Text(string.Join("  ·  ", new[] { school.Phone, school.Email, school.Website }.Where(x => !string.IsNullOrWhiteSpace(x)))).FontSize(7);
                c.Item().PaddingTop(4).AlignCenter().Text($"{card.Term.ToUpperInvariant()} REPORT  ·  {card.Session} SESSION").FontSize(10).Bold();
            });
            row.ConstantItem(62).Height(62).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Element(e =>
            {
                if (photo is { Length: > 0 }) e.Image(photo).FitArea();
                else e.AlignCenter().AlignMiddle().Text("PHOTO").FontSize(7).FontColor(Colors.Grey.Medium);
            });
        });

    private static void ComposeDetails(IContainer container, ReportCard card)
    {
        string Absent() => card.DaysAbsent?.ToString(CultureInfo.InvariantCulture) ?? "—";
        (string Label, string Value)[] cells =
        [
            ("Name", card.StudentName), ("Admission no.", card.AdmissionNumber), ("House", card.House ?? "—"),
            ("Date of birth", card.DateOfBirth?.ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? "—"), ("Class", card.ClassRoom), ("Sex", card.Sex?.ToString() ?? "—"),
            ("Times school opened", card.DaysOpened?.ToString(CultureInfo.InvariantCulture) ?? "—"), ("Times present", card.DaysPresent?.ToString(CultureInfo.InvariantCulture) ?? "—"), ("Times absent", Absent()),
        ];
        container.Table(table =>
        {
            table.ColumnsDefinition(c => { for (var i = 0; i < 3; i++) { c.ConstantColumn(78); c.RelativeColumn(); } });
            foreach (var (label, value) in cells)
            {
                table.Cell().Background(Fill).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(label.ToUpperInvariant()).FontSize(6.5f).Bold();
                table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(value);
            }
        });
    }

    private static void ComposeSubjects(IContainer container, ReportCard card)
    {
        var compare = card.PreviousTerms.Count > 0;
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.4f);                                    // subject
                foreach (var _ in card.Components) c.RelativeColumn(1.2f);  // component scores
                c.RelativeColumn(1.1f);                                    // total
                c.RelativeColumn(0.9f);                                    // grade
                c.RelativeColumn(1.45f);                                   // remark
                c.RelativeColumn(1.1f);                                    // class average
                if (compare) { c.RelativeColumn(card.PreviousTerms.Count == 1 ? 1.1f : 1.6f); c.RelativeColumn(1.35f); } // previous, change
                c.RelativeColumn(compare ? 3.6f : 4.6f);                   // comment
            });

            table.Header(h =>
            {
                // Two short lines per heading (name, then max) so words never break mid-word.
                void Head(string text, string? sub = null) => h.Cell().Background(Navy).PaddingVertical(3).PaddingHorizontal(1).AlignCenter().AlignMiddle().Column(c =>
                {
                    c.Item().AlignCenter().Text(text).FontSize(text.Length > 8 ? 4.4f : 5f).Bold().FontColor(Colors.White);
                    if (sub is not null) c.Item().AlignCenter().Text(sub).FontSize(6).FontColor(Colors.White);
                });
                Head("SUBJECT");
                foreach (var c in card.Components) Head(c.Name.ToUpperInvariant(), $"({c.MaxScore:0.##})");
                Head("TOTAL", "(100)"); Head("GRADE"); Head("REMARK"); Head("CLASS", "AVG");
                if (compare) { Head(card.PreviousTerms.Count == 1 ? $"{Short(card.PreviousTerms[0].Number)}" : "EARLIER", "TERM"); Head("CHANGE"); }
                Head("TEACHER'S COMMENT");
            });

            var index = 0;
            foreach (var line in card.Subjects)
            {
                // Alternate rows are tinted; the others stay transparent so a DRAFT watermark shows through.
                var tinted = index++ % 2 == 1;
                IContainer Cell()
                {
                    IContainer cell = table.Cell();
                    if (tinted) cell = cell.Background("#f3f6fa");
                    return cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(2.5f);
                }
                Cell().Text(line.Subject.ToUpperInvariant()).Bold();
                foreach (var score in line.Scores) Cell().AlignCenter().Text(score.Score?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—");
                Cell().AlignCenter().Text(line.Total.ToString("0.##", CultureInfo.InvariantCulture)).Bold();
                Cell().AlignCenter().Text(line.Grade ?? "—").Bold();
                Cell().Text(line.Remark ?? "");
                Cell().AlignCenter().Text(line.ClassAverage.ToString("0.##", CultureInfo.InvariantCulture));
                if (compare)
                {
                    Cell().AlignCenter().Text(line.PreviousTerms.Count == 0 ? "—"
                        : string.Join("  ", line.PreviousTerms.Select(p => card.PreviousTerms.Count == 1 ? p.Total.ToString("0.##", CultureInfo.InvariantCulture) : $"{Short(p.Number)}: {p.Total:0.##}")));
                    Cell().AlignCenter().Element(e => Change(e, line.ChangeFromLastTerm, line.Trend));
                }
                Cell().Text(line.Comment ?? "").FontSize(6.5f);
            }
        });
    }

    private static void ComposeSummary(IContainer container, ReportCard card) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(Fill).Padding(5).Row(row =>
        {
            void Figure(string label, string value) => row.RelativeItem().Column(c =>
            {
                c.Item().AlignCenter().Text(label).FontSize(6.5f).Bold().FontColor(Navy);
                c.Item().AlignCenter().Text(value).FontSize(11).Bold();
            });
            Figure("CUMULATIVE TOTAL", card.TotalScore.ToString("0.##", CultureInfo.InvariantCulture));
            Figure("PUPIL'S AVERAGE", card.Average?.ToString("0.00", CultureInfo.InvariantCulture) ?? "—");
            Figure("CLASS AVERAGE", card.ClassAverage.ToString("0.00", CultureInfo.InvariantCulture));
            Figure("POSITION", card.Position is { } p ? $"{Ordinal(p)} of {card.ClassSize}" : "—");
            if (card.CumulativeAverage is { } annual) Figure("ANNUAL AVERAGE", annual.ToString("0.00", CultureInfo.InvariantCulture));
        });

    private static void ComposeComparison(IContainer container, ReportCard card) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(5).Column(col =>
        {
            col.Item().Text("TERM COMPARISON").FontSize(7).Bold().FontColor(Navy);
            col.Item().PaddingTop(3).Row(row =>
            {
                foreach (var previous in card.PreviousTerms)
                    row.RelativeItem().Text(t =>
                    {
                        t.Span($"{previous.Term}: ").Bold();
                        t.Span($"average {previous.Average:0.00}");
                        if (previous.Position is { } p) t.Span($", {Ordinal(p)} of {previous.ClassSize}");
                    });
                row.RelativeItem().Text(t =>
                {
                    t.Span("This term: ").Bold();
                    t.Span(card.Average is { } a ? $"average {a:0.00}" : "—");
                    if (card.AverageChange is { } change)
                    {
                        t.Span("  ");
                        t.Span(TrendText(change, card.Trend)).Bold().FontColor(TrendColour(card.Trend));
                    }
                });
            });
        });

    private static void ComposeGradingKey(IContainer container, ReportCard card) =>
        container.Text(t =>
        {
            t.Span("GRADING KEY: ").Bold().FontSize(7);
            t.Span(string.Join("   ", card.GradingKey.Select(b => $"{b.Grade} {b.MinScore:0.##}–{b.MaxScore:0.##} {b.Remark}"))).FontSize(7);
        });

    /// <summary>Affective and psychomotor traits in two columns, then clubs and the A–E key (E2.5).</summary>
    private static void ComposeBehaviour(IContainer container, ReportCard card) =>
        container.Row(row =>
        {
            if (card.Behaviour.Count > 0)
            {
                row.RelativeItem(3).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Column(col =>
                {
                    col.Item().Text("AFFECTIVE & PSYCHOMOTOR TRAITS").FontSize(7).Bold().FontColor(Navy);
                    col.Item().PaddingTop(2).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(16); c.ConstantColumn(10); c.RelativeColumn(); c.ConstantColumn(16); });
                        var half = (card.Behaviour.Count + 1) / 2;
                        for (var i = 0; i < half; i++)
                        {
                            Trait(table, card.Behaviour[i]);
                            table.Cell();
                            if (i + half < card.Behaviour.Count) Trait(table, card.Behaviour[i + half]);
                            else { table.Cell(); table.Cell(); }
                        }
                    });
                });
                row.ConstantItem(6);
            }
            row.RelativeItem(2).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Column(col =>
            {
                col.Item().Text("CLUBS & ACTIVITIES").FontSize(7).Bold().FontColor(Navy);
                if (card.Clubs.Count == 0) col.Item().PaddingTop(2).Text("—");
                foreach (var club in card.Clubs)
                    col.Item().PaddingTop(2).Text(t =>
                    {
                        t.Span(club.Name.ToUpperInvariant()).Bold();
                        if (club.Rating is { } rating) t.Span($"  {rating.ToUpperInvariant()}");
                    });
                if (card.Behaviour.Count > 0)
                {
                    col.Item().PaddingTop(5).Text("RATING KEY").FontSize(6.5f).Bold().FontColor(Navy);
                    foreach (var key in card.BehaviourKey)
                        col.Item().Text($"{key.Rating} – {key.Meaning}").FontSize(6.5f);
                }
            });
        });

    private static void Trait(TableDescriptor table, TraitRatingLine trait)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(1.2f).Text(trait.Trait).FontSize(6.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(1.2f).AlignCenter().Text(trait.Rating ?? "").FontSize(7).Bold();
    }

    private static void ComposeComments(IContainer container, ReportCard card, ReportBranding school) =>
        container.Row(row =>
        {
            void Box(string title, string? comment, string? signer, byte[]? signature = null) => row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(5).Column(c =>
            {
                c.Item().Text(title).FontSize(7).Bold().FontColor(Navy);
                c.Item().PaddingTop(2).MinHeight(30).Text(comment ?? "");
                if (signature is { Length: > 0 }) c.Item().PaddingTop(2).Height(22).Image(signature).FitArea();
                c.Item().PaddingTop(4).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingTop(2).Text(signer ?? " ").FontSize(7).Italic();
            });
            Box("FORM TEACHER'S COMMENT", card.FormTeacherComment, card.FormTeacher);
            row.ConstantItem(6);
            Box("PRINCIPAL'S COMMENT", card.PrincipalComment, school.PrincipalName, school.Signature);
        });

    private static void Change(IContainer container, decimal? change, Trend? trend)
    {
        if (change is not { } value) { container.Text("—"); return; }
        container.Text(TrendText(value, trend)).Bold().FontColor(TrendColour(trend));
    }

    private static string TrendText(decimal change, Trend? trend) => trend switch
    {
        Trend.Improved => $"+{change:0.##}",
        Trend.Declined => $"-{Math.Abs(change):0.##}",
        _ => "Steady",
    };

    private static string TrendColour(Trend? trend) => trend switch
    {
        Trend.Improved => "#1f7a3d",
        Trend.Declined => "#b42332",
        _ => "#5f6b78",
    };

    private static string Short(Domain.Academics.TermNumber n) => n switch
    {
        Domain.Academics.TermNumber.First => "1ST",
        Domain.Academics.TermNumber.Second => "2ND",
        _ => "3RD",
    };

    private static string Ordinal(int n) => (n % 100) switch
    {
        11 or 12 or 13 => $"{n}th",
        _ => (n % 10) switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" },
    };
}
