using System;

namespace AISO.Domain.Approvals;

public record DelegateApprovalDto(
    string RequestingTeamsUser,
    string DelegateUser,
    string? SalesOrg,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    string? Reason,
    decimal? MaxAmount = null,
    string? Currency = "VND");
