using System;

namespace AISO.Domain.Entities;

/// <summary>
/// Maps a Microsoft Teams User to a SAP System Username for Single Sign-On and Authorization.
/// </summary>
public class UserMapping
{
    public Guid Id { get; set; }

    /// <summary>
    /// The Entra ID Object ID or Teams User AAD Object ID.
    /// </summary>
    public required string TeamsUserId { get; set; }

    /// <summary>
    /// The user's email address in Microsoft Teams/Entra ID.
    /// </summary>
    public string? TeamsEmail { get; set; }

    /// <summary>
    /// The corresponding username in the SAP S/4HANA system.
    /// </summary>
    public required string SapUsername { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
