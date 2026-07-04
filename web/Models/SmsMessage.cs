namespace web.Models;

using web.Utils;

// Lokal log over sms'er — både sendte (Outbound) og modtagne (Inbound, via
// webhook fra gatewayen). En Outbound-række skrives KUN efter et vellykket
// kald til ISmsService.SendSmsAsync, så MessageId og VolunteerId altid er
// parret for udgående sms'er. Al sms-afsendelse i systemet skal gå via
// ISmsMessageLogService.SendAndLogAsync, som er ansvarlig for at skrive den.
// Inbound-rækker skrives af SmsWebhookController og har intet MessageId fra
// gatewayen, og VolunteerId er kun sat hvis afsendernummeret matcher en
// frivillig i indeværende sæson.
public class SmsMessage
{
    public int Id { get; set; }
    public int SeasonId { get; set; }

    public SmsDirection Direction { get; set; } = SmsDirection.Outbound;

    // Gatewayens MessageId for udgående sms'er — null for indgående, da
    // gatewayen ikke leverer et globalt id for modtagne sms'er via webhook.
    public Guid? MessageId { get; set; }

    // Den frivillige sms'en blev sendt til (Outbound), eller som blev matchet
    // ud fra afsendernummeret (Inbound) — null hvis nummeret ikke matcher en
    // frivillig i indeværende sæson.
    public int? VolunteerId { get; set; }
    public Volunteer? Volunteer { get; set; }

    // Modtager (Outbound) eller afsender (Inbound) telefonnummer.
    public string PhoneNumberSnapshot { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    // Kun relevant for Inbound — om gatewayens modem endnu ikke har markeret
    // sms'en som læst ("REC UNREAD"). Vises bevidst ikke i sms-fanen under
    // Administration; er til fremtidig brug (fx en ulæst-tæller).
    public bool IsUnread { get; set; }

    // Koordinatorens egen læst-status i Besked Center — adskilt fra IsUnread
    // ovenfor, som er gatewayens modem-state. Default true, da Outbound-rækker
    // (koordinator har selv skrevet dem) er læst fra start; sættes false kun
    // på nye Inbound-rækker fra SmsWebhookController.
    public bool IsReadByCoordinator { get; set; } = true;
    public DateTime? ReadByCoordinatorAt { get; set; }

    public int SegmentCount { get; set; }
    public decimal UnitPriceDkk { get; set; }
    public decimal TotalPriceDkk { get; set; }

    // Tidspunkt fra gatewayen: køet (Outbound) eller modtaget (Inbound).
    public DateTime OccurredAt { get; set; }

    // Hvilken admin sendte den (Outbound) — null for Inbound.
    public string? SentByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = AppTime.Now;
}

public enum SmsDirection
{
    Inbound = 0,
    Outbound = 1
}
