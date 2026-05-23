namespace web.Models;

public class KortStederViewModel
{
    public List<MapLocation> Items { get; set; } = [];
    public string Q { get; set; } = "";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int RangeFrom { get; set; }
    public int RangeTo { get; set; }
}
