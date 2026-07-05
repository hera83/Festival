namespace web.Models;

public class AdminUserRow
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public IList<string> Roles { get; set; } = [];
    public bool IsLockedOut { get; set; }
    public DateTime? LastLogin { get; set; }
}

public class AdminUsersViewModel
{
    public IList<AdminUserRow> Users { get; set; } = [];
    public IList<string> AllRoles { get; set; } = [];
    public string CurrentUserId { get; set; } = string.Empty;

    // Paginering
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;
    public int RangeFrom => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int RangeTo => Math.Min(Page * PageSize, TotalCount);
}

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public List<string> SelectedRoles { get; set; } = [];
    public bool IsLockedOut { get; set; }
    public string? NewPassword { get; set; }
}

public class CreateUserViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Password { get; set; } = string.Empty;
    public string? SelectedRole { get; set; }
}
