namespace web.Models;

public class VolunteerImportPreviewRowViewModel
{
    public int RowNumber { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public string ShiftName { get; set; } = "Diverse";
    public List<string> Errors { get; set; } = [];

    public bool HasErrors => Errors.Count > 0;
}

public class VolunteerImportPreviewViewModel
{
    public string FileName { get; set; } = string.Empty;
    public IList<VolunteerImportPreviewRowViewModel> Rows { get; set; } = [];
    public int TotalCount => Rows.Count;
    public int ErrorCount => Rows.Count(r => r.HasErrors);
    public int ValidCount => Rows.Count(r => !r.HasErrors);
}
