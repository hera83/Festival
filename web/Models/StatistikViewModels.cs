namespace web.Models;

public class StatistikVolunteerRow
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }

    /// <summary>Antal afsluttede check-in sessioner.</summary>
    public int CheckInCount { get; set; }

    /// <summary>Total indchecket tid i timer (kun lukkede sessioner).</summary>
    public double TotalHours { get; set; }
}

public class LocationEventDto
{
    public string Time { get; set; } = string.Empty;      // "08:32"
    public string EventType { get; set; } = string.Empty; // "CheckIn", "Move", "CheckOut"
    public string? Location { get; set; }                  // "Pit", "Bar8", null ved CheckOut
}

public class DaySessionDto
{
    public string Date { get; set; } = string.Empty;
    public string CheckInTime { get; set; } = string.Empty;
    public string? CheckOutTime { get; set; }
    public double DurationHours { get; set; }
    public double LeftPct { get; set; }
    public double WidthPct { get; set; }
    public bool IsOpen { get; set; }
    public IList<LocationEventDto> Locations { get; set; } = [];
}

public class VolunteerDetailDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double TotalHours { get; set; }
    public IList<DaySessionDto> Sessions { get; set; } = [];
}

public class StatistikViewModel
{
    public IList<StatistikVolunteerRow> Rows { get; set; } = [];

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;
    public int RangeFrom => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int RangeTo => Math.Min(Page * PageSize, TotalCount);
}
