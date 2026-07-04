namespace web.Models;

using web.Utils;

// Skabeloner for de automatiske sms'er der sendes til en frivillig når
// koordinatoren checker vedkommende ind/ud eller flytter dem i dashboardet
// (manuelt eller via en planlagt flytning). Se IDashboardSmsFlowService for
// selve afsendelseslogikken.
public class SmsTemplate
{
    public int Id { get; set; }
    public int SeasonId { get; set; }

    public SmsTemplateType Type { get; set; }
    public string Body { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = AppTime.Now;
}

public enum SmsTemplateType
{
    CheckIn = 0,
    CheckOut = 1,
    Moved = 2
}

// Udgangspunktsteksten for hver skabelontype, indtil koordinatoren gemmer sin egen.
// Fletfelterne ({{Navn}} osv.) erstattes først når afsendelseslogikken kobles på.
public static class SmsTemplateDefaults
{
    public const string CheckIn = "Hej {{Navn}}, du er nu checket ind til {{Post}}. Tak fordi du hjælper til i dag!";
    public const string CheckOut = "Hej {{Navn}}, du er nu checket ud fra {{Post}}. Tak for din indsats i dag – vi ses igen snart!";
    public const string Moved = "Hej {{Navn}}, du er blevet flyttet fra {{FraPost}} til {{TilPost}}. Mød op på den nye post hurtigst muligt.";

    public static string For(SmsTemplateType type) => type switch
    {
        SmsTemplateType.CheckIn => CheckIn,
        SmsTemplateType.CheckOut => CheckOut,
        SmsTemplateType.Moved => Moved,
        _ => ""
    };
}

// Nøglerne i DashboardSettings der styrer om check-in/check-ud/flytning sender sms.
// Delt mellem DashboardController (knappen + StateHash), DashboardSmsFlowService
// og SmsStatusUpdateService (automatisk sluk ved tom saldo).
public static class SmsFlowSetting
{
    public const string Key = "SmsFlowEnabled";

    // Tidspunkt (DateTime.Ticks) for seneste automatiske sluk pga. for lav saldo.
    // Bruges af klienten til at vise en engangs-toast uden at afsenderen skal
    // rydde værdien igen (så alle åbne faner selv opdager og viser den én gang).
    public const string AutoOffAtKey = "SmsFlowAutoOffAt";
}
