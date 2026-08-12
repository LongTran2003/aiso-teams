namespace AISO.Domain.Approvals;

public record RevokeDelegationDto(
    string RequestingTeamsUser,
    string DelegationId);
