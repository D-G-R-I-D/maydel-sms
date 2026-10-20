using System.Net.Mail;
using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.Contact;

/// <summary>The website's contact form (E7.1): stored for the office and forwarded to the school's email address.</summary>
public sealed record SendContactMessageCommand(ContactInput Message) : ICommand;

public sealed record ListContactMessagesQuery(MessageStatus? Status = null, int? Page = null, int? PageSize = null) : IQuery<PagedResult<ContactMessageDto>>;

public sealed record SetContactStatusCommand(Guid Id, MessageStatus Status) : ICommand<ContactMessageDto>;

internal sealed class SendContactMessageHandler(IAppDbContext db, TimeProvider time) : ICommandHandler<SendContactMessageCommand>
{
    public async Task<Result> Handle(SendContactMessageCommand c, CancellationToken ct)
    {
        var m = c.Message;
        // Honeypot: real visitors never fill the hidden "website" field. Pretend success so bots learn nothing.
        if (!string.IsNullOrWhiteSpace(m.Website)) return Result.Success();
        var name = (m.Name ?? "").Trim();
        var email = (m.Email ?? "").Trim();
        var text = (m.Message ?? "").Trim();
        if (name.Length is 0 or > 120) return Error.Validation("name", "Enter your name.");
        if (email.Length > 254 || !MailAddress.TryCreate(email, out _)) return Error.Validation("email", "Enter a valid email address so we can reply.");
        if (text.Length is < 5 or > 4000) return Error.Validation("message", "Write your message (up to 4000 characters).");
        if (m.Phone is { Length: > 30 }) return Error.Validation("phone", "Enter a valid phone number.");
        if (m.Subject is { Length: > 150 }) return Error.Validation("subject", "Keep the subject under 150 characters.");

        var message = new ContactMessage
        {
            Name = name, Email = email.ToLowerInvariant(), Phone = string.IsNullOrWhiteSpace(m.Phone) ? null : m.Phone.Trim(),
            Subject = string.IsNullOrWhiteSpace(m.Subject) ? null : m.Subject.Trim(), Message = text,
        };
        db.ContactMessages.Add(message);
        var school = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).Select(p => new { p.Email, p.Name }).FirstOrDefaultAsync(ct);
        if (school?.Email is { } to)
            db.OutboxEmails.Add(new OutboxEmail
            {
                Kind = EmailKind.ContactForm, ToAddress = to, ToName = school.Name, ReplyTo = message.Email,
                Subject = $"Website message: {message.Subject ?? "from " + message.Name}"[..Math.Min(200, $"Website message: {message.Subject ?? "from " + message.Name}".Length)],
                Body = $"From: {message.Name} <{message.Email}>{(message.Phone is null ? "" : $", {message.Phone}")}\n\n{message.Message}\n\n" +
                       "Press Reply to answer the sender. This message is also in the staff console under Website → Messages.",
                NextAttemptAt = time.GetUtcNow(),
            });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class ListContactMessagesHandler(IAppDbContext db) : IQueryHandler<ListContactMessagesQuery, PagedResult<ContactMessageDto>>
{
    public async Task<Result<PagedResult<ContactMessageDto>>> Handle(ListContactMessagesQuery q, CancellationToken ct)
    {
        var messages = db.ContactMessages.AsNoTracking();
        if (q.Status is { } status) messages = messages.Where(m => m.Status == status);
        return await messages.OrderByDescending(m => m.CreatedAt)
            .Select(m => new ContactMessageDto(m.Id, m.Name, m.Email, m.Phone, m.Subject, m.Message, m.Status, m.CreatedAt)).ToPagedAsync(q.Page, q.PageSize, ct);
    }
}

internal sealed class SetContactStatusHandler(IAppDbContext db) : ICommandHandler<SetContactStatusCommand, ContactMessageDto>
{
    public async Task<Result<ContactMessageDto>> Handle(SetContactStatusCommand c, CancellationToken ct)
    {
        if (await db.ContactMessages.FirstOrDefaultAsync(m => m.Id == c.Id, ct) is not { } m) return WebsiteErrors.MessageNotFound;
        if (!Enum.IsDefined(c.Status)) return Error.Validation("status", "Choose New, Read, Replied or Archived.");
        m.Status = c.Status;
        await db.SaveChangesAsync(ct);
        return new ContactMessageDto(m.Id, m.Name, m.Email, m.Phone, m.Subject, m.Message, m.Status, m.CreatedAt);
    }
}
