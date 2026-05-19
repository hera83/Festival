namespace web.Models;

using web.Utils;

/// <summary>
/// En post er en navngivet gruppe/zone på festivalen (fx "Bar 8", "Blå Scene").
/// Frivillige kan flyttes fra Pitten til en post og mellem poster.
/// </summary>
public class Post
{
    public int Id { get; set; }
    public int SeasonId { get; set; }

    /// <summary>Navn på posten, fx "Bar 8" eller "Blå Scene".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Hvilken drop-zone spalte posten befinder sig i (1–4).</summary>
    public int ColumnIndex { get; set; } = 1;

    /// <summary>Rækkefølge inden for spalten.</summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// Antal minutter en frivillig må opholde sig på posten før alarm udløses.
    /// Null = ingen alarm.
    /// </summary>
    public int? AlarmAfterMinutes { get; set; }

    public DateTime CreatedAt { get; set; } = AppTime.Now;
}
