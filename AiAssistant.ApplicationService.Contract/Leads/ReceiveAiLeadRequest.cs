namespace AiAssistant.ApplicationService.Contract.Leads;

public sealed record ReceiveAiLeadRequest(
    long SourceLeadAssignmentId,
    string PhoneNumber,
    string? UserName);
