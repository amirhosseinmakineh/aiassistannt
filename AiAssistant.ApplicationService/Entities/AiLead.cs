namespace AiAssistant.ApplicationService.Entities;

public sealed class AiLead
{
    public long Id { get; set; }
    public long SourceLeadAssignmentId { get; set; }
    public string PhoneNumber { get; set; } = default!;
    public string? UserName { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ReportSubmittedAt { get; set; }
    public string? ReportDescription { get; set; }
}
