namespace web.Models;

// ── Opret frivillig ──────────────────────────────────────────────
public class CreateVolunteerViewModel
{
    public string Key         { get; set; } = string.Empty;
    public string Name        { get; set; } = string.Empty;
    public string? Email      { get; set; }
    public string? PhoneNumber { get; set; }
    public List<int> ShiftTypeIds { get; set; } = [];
    public List<ShiftType> AvailableShiftTypes { get; set; } = [];
}

// ── Rediger frivillig ────────────────────────────────────────────
public class EditVolunteerViewModel
{
    public int    Id          { get; set; }
    public string Key         { get; set; } = string.Empty;
    public string Name        { get; set; } = string.Empty;
    public string? Email      { get; set; }
    public string? PhoneNumber { get; set; }
    public List<int> ShiftTypeIds { get; set; } = [];
    public List<ShiftType> AvailableShiftTypes { get; set; } = [];
}

// ── Fælles paginerings-base ──────────────────────────────────────
public abstract class PagedViewModelBase
{
    public int Page      { get; set; } = 1;
    public int PageSize  { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages  => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;
    public int RangeFrom   => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int RangeTo     => Math.Min(Page * PageSize, TotalCount);
    public string Query  { get; set; } = string.Empty;
}

// ── Frivillige ───────────────────────────────────────────────────
public class VolunteersPagedViewModel : PagedViewModelBase
{
    public IList<VolunteerRowViewModel> Volunteers { get; set; } = [];
}

public class VolunteerRowViewModel
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? LastAppUsedAt { get; set; }
    public DateTime? AppInstalledAt { get; set; }
    public string? AppDeviceName { get; set; }
    public bool AppInstalled => AppInstalledAt.HasValue || LastAppUsedAt.HasValue;
}

// ── Vagttyper ────────────────────────────────────────────────────
public class ShiftTypesPagedViewModel : PagedViewModelBase
{
    public IList<ShiftType> ShiftTypes { get; set; } = [];
}

public class ShiftTypeFormViewModel
{
    public int Id { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int RequiredCount { get; set; }
}

// ── Vagter ───────────────────────────────────────────────────────
public class ShiftsPagedViewModel : PagedViewModelBase
{
    public IList<Shift> Shifts { get; set; } = [];
}

// ── Behov ────────────────────────────────────────────────────────
public class BehovRow
{
    public int    ShiftTypeId   { get; set; }
    public string ShiftName     { get; set; } = string.Empty;
    public DateTime StartTime   { get; set; }
    public DateTime EndTime     { get; set; }
    public int    RequiredCount { get; set; }
    public int    SignedUpCount { get; set; }
    public int    Missing       => Math.Max(0, RequiredCount - SignedUpCount);
}

public class BehovPagedViewModel : PagedViewModelBase
{
    public IList<BehovRow> Rows { get; set; } = [];
    public int TotalMissing     { get; set; }
}
