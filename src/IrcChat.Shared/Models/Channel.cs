// Dans IrcChat.Shared/Models/Channel.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace IrcChat.Shared.Models;

public class Channel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsMuted { get; set; }

    /// <summary>
    /// Gets or sets utilisateur qui gère actuellement le salon (pour l'auto-mute).
    /// Par défaut, c'est le créateur. Un admin peut prendre le relais en démutant.
    /// </summary>
    public string? ActiveManager { get; set; }

    public string? Description { get; set; }

    [NotMapped]
    public int ConnectedUsersCount { get; set; }
}