using Maydel.Domain.Common;

namespace Maydel.Domain.Admissions;

public enum ApplicationStatus
{
    New,
    Contacted,
    AssessmentScheduled,
    Offered,
    Enrolled,
    Declined,
}

/// <summary>An enquiry submitted from the public admissions form, tracked through to enrolment.</summary>
public class AdmissionApplication : Entity
{
    public required string ReferenceNumber { get; set; }
    public required string ParentName { get; set; }
    public required string Phone { get; set; }
    public string? Email { get; set; }
    public required string ChildName { get; set; }
    public int ChildAge { get; set; }
    public required string ClassInterested { get; set; }
    public string? Message { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.New;
    public string? StaffNotes { get; set; }

    /// <summary>Set when the application is converted into an enrolled student.</summary>
    public Guid? StudentId { get; set; }
}

public enum MessageStatus
{
    New,
    Read,
    Replied,
    Archived,
}

public class ContactMessage : Entity
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? Subject { get; set; }
    public required string Message { get; set; }
    public MessageStatus Status { get; set; } = MessageStatus.New;
}

public class NewsletterSubscriber : Entity
{
    public required string Email { get; set; }
    public bool IsActive { get; set; } = true;
}
