namespace web.Models;

using web.Utils;

/// <summary>
/// Et navngivet GPS-punkt på festivalpladsen (fx "Pit", "Blå Scene", "Post 3").
/// Vises som POI-lag på kortvisningen. Sæsonafhængig.
/// </summary>
public class MapLocation
{
    public int Id { get; set; }
    public int SeasonId { get; set; }

    /// <summary>Navn på stedet, fx "Pit" eller "Blå Scene".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Kategori: Pit, Post, Scene, Toiletter, Førstehjælp, Parkering, Andet.</summary>
    public string Category { get; set; } = "Andet";

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Valgfri beskrivelse / note til stedet.</summary>
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = AppTime.Now;
}
