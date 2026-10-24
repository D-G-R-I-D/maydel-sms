using System.Net.Mail;
using Maydel.Application.Abstractions;
using Maydel.Infrastructure.Notifications;

namespace Maydel.Api.Tests.Notifications;

/// <summary>The branded HTML layout every email uses: details tables, link buttons, the embedded logo, and escaping.</summary>
public sealed class EmailLayoutTests
{
    private static readonly EmailBrand Brand = new("Maydel Spring Schools", "Wellspring of Knowledge", "Plot 12, Abuja", "0800 000 0000",
        "info@maydel.sch.ng", "maydel.sch.ng", null, "Fees invoice");

    private const string Invoice = """
        Dear Mrs. Ada Obi,

        Ada's fees invoice for the First Term, 2026/2027 session, is ready. The invoice is attached as a PDF.

        Invoice: INV-2026-2027-00012
        Total: NGN 250,000.00
        Due by: 14 September 2026

        When paying by bank transfer or at the bank, please quote the admission number MSS/2026/0001 as the reference.

        You can see the invoice, pay online and download receipts in the parent portal: https://portal.maydel.sch.ng

        Maydel Spring Schools
        """;

    [Fact]
    public void Lines_of_details_become_a_table_and_the_portal_link_a_button()
    {
        var html = MailMessages.Html(Invoice, Brand);

        Assert.Contains("<h1", html);
        Assert.Contains("Fees invoice", html);
        Assert.Contains(">Invoice</td>", html);
        Assert.Contains(">NGN 250,000.00</td>", html);
        Assert.Contains("href=\"https://portal.maydel.sch.ng\"", html);
        Assert.Contains(">Open the parent portal</a>", html);
        Assert.Contains("cid:" + MailMessages.LogoContentId, html);
        Assert.Contains("Wellspring of Knowledge", html);
        Assert.Contains("0800 000 0000", html);
    }

    [Fact]
    public void Text_from_people_is_escaped_and_odd_links_are_not_trusted()
    {
        var html = MailMessages.Html("Dear <script>alert(1)</script>,\n\nName: <b>Bold</b>\n\nSee javascript:alert(1) and \"https://x.test/a\"onmouseover=\"y\"", Brand);

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>Bold", html);
        Assert.DoesNotContain("href=\"javascript", html);
        Assert.DoesNotContain("onmouseover=\"", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void The_logo_travels_inside_the_email_and_the_text_part_is_unchanged()
    {
        using var mail = MailMessages.Build(new EmailMessage("parent@example.com", "Mrs. Obi", "Fees", Invoice, [], Brand: Brand),
            new EmailOptions { FromAddress = "no-reply@school.ng", FromName = "Maydel" });

        Assert.Equal(Invoice, mail.Body);
        var html = Assert.Single(mail.AlternateViews);
        var logo = Assert.Single(html.LinkedResources);
        Assert.Equal(MailMessages.LogoContentId, logo.ContentId);
        Assert.Equal("image/png", logo.ContentType.MediaType);

        // Optional: write sample emails to look at (set MAYDEL_EMAIL_PREVIEW to a folder).
        if (Environment.GetEnvironmentVariable("MAYDEL_EMAIL_PREVIEW") is { Length: > 0 } folder) Preview.Write(folder, Brand);
    }

    private static class Preview
    {
        public static void Write(string folder, EmailBrand brand)
        {
            Directory.CreateDirectory(folder);
            var logo = MailMessages.Logo(brand)!.Value;
            var src = $"data:{logo.ContentType};base64,{Convert.ToBase64String(logo.Bytes)}";
            void Save(string name, string heading, string text) =>
                File.WriteAllText(Path.Combine(folder, name), MailMessages.Html(text, brand with { Heading = heading }).Replace("cid:" + MailMessages.LogoContentId, src));

            Save("1-invoice.html", "Fees invoice", Invoice);
            Save("2-payment-received.html", "Payment received", """
                Dear Mrs. Ada Obi,

                Thank you. We've received a payment for Ada Obi (MSS/2026/0001). Your receipt is attached.

                Amount: NGN 150,000.00
                Receipt: RCT-2026-2027-00031
                Paid on: 24 October 2026
                Invoice: INV-2026-2027-00012
                Balance: NGN 100,000.00

                You can see invoices, pay online and download receipts in the parent portal: https://portal.maydel.sch.ng

                Maydel Spring Schools
                """);
            Save("3-fees-overdue.html", "Fees overdue", """
                Dear Mrs. Ada Obi,

                Ada's First Term 2026/2027 fees are overdue.

                Overdue: NGN 150,000.00
                Balance: NGN 250,000.00
                Invoice: INV-2026-2027-00012
                Reference: MSS/2026/0001

                Please pay as soon as you can, quoting the reference above. If you've already paid, please send the proof of payment in the parent portal or contact the accounts office, and ignore this reminder.

                You can see invoices, pay online and download receipts in the parent portal: https://portal.maydel.sch.ng

                Maydel Spring Schools
                """);
            Save("4-report-card.html", "Report card", """
                Dear Mrs. Ada Obi,

                Ada's report card for the First Term, 2026/2027 session, is ready. It is attached as a PDF.

                You can also see it in the parent portal: https://portal.maydel.sch.ng

                Maydel Spring Schools
                """);
        }
    }
}
