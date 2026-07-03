namespace web.Models;

public class SmsGatewayStatusViewModel
{
    public bool GatewayOnline { get; set; }
    public string? HealthStatus { get; set; }
    public DateTime? HealthTimestamp { get; set; }
    public string? GatewayErrorMessage { get; set; }

    public decimal? Balance { get; set; }
    public DateTime? BalanceUpdatedAt { get; set; }
}

public class SmsPartialViewModel
{
    public SmsGatewayStatusViewModel Status { get; set; } = new();

    public SmsLogViewModel Log { get; set; } = new();

    // Indeværende sæsons frivillige med telefonnummer — bruges til checkbox-vælgeren
    // i "Opret/rediger abonnementsliste"-modalerne.
    public List<SmsVolunteerPickerItem> AllVolunteers { get; set; } = [];
}

public class SmsLogViewModel
{
    public List<SmsMessageRowViewModel> Items { get; set; } = [];
    public string Q { get; set; } = "";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int RangeFrom { get; set; }
    public int RangeTo { get; set; }
}

public class SmsMessageRowViewModel
{
    public int Id { get; set; }
    public SmsDirection Direction { get; set; }
    public Guid? MessageId { get; set; }
    public int? VolunteerId { get; set; }
    public string? VolunteerName { get; set; }
    public string PhoneNumberSnapshot { get; set; } = "";
    public string MessageBody { get; set; } = "";
    public string Status { get; set; } = "";
    public int SegmentCount { get; set; }
    public decimal TotalPriceDkk { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? SentByDisplayName { get; set; }
}

public class SmsVolunteerPickerItem
{
    public int VolunteerId { get; set; }
    public string Name { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
}

public class SmsAfsendModtagereViewModel
{
    public List<SmsVolunteerPickerItem> Items { get; set; } = [];
    public string? Error { get; set; }
}

public class SmsSubscriptionRowViewModel
{
    public Guid Id { get; set; }
    public List<string> PhoneNumbers { get; set; } = [];
    public List<SmsVolunteerPickerItem> MatchedVolunteers { get; set; } = [];
    public List<string> UnmatchedPhoneNumbers { get; set; } = [];
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? WebhookUrl { get; set; }
    public bool IsActive { get; set; }
    public bool IsCurrentlyInWindow { get; set; }
}

public class SmsSubscriptionListViewModel
{
    public List<SmsSubscriptionRowViewModel> Items { get; set; } = [];
    public string? Error { get; set; }
}

public class SmsVolunteerCheckboxPickerModel
{
    public List<SmsVolunteerPickerItem> Volunteers { get; set; } = [];
    public string FilterInputId { get; set; } = "";
    public string ListId { get; set; } = "";
}
