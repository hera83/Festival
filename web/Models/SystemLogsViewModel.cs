namespace web.Models;

public class SystemLogEntry
{
    public long   Id              { get; set; }
    public string Timestamp       { get; set; } = "";
    public string Level           { get; set; } = "";
    public string RenderedMessage { get; set; } = "";
    public string? Exception      { get; set; }
    public string? Properties     { get; set; }
}

public class SystemLogsViewModel
{
    public List<SystemLogEntry> Rows       { get; set; } = new();
    public string               Q          { get; set; } = "";
    public string               Level      { get; set; } = "";
    public string               DateFrom   { get; set; } = "";
    public string               DateTo     { get; set; } = "";
    public bool                 OnlyErrors { get; set; }
    public int                  Page       { get; set; } = 1;
    public int                  PageSize   { get; set; } = 10;
    public int                  TotalCount { get; set; }
    public int                  TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;
    public int                  RangeFrom  => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int                  RangeTo    => Math.Min(Page * PageSize, TotalCount);
}
