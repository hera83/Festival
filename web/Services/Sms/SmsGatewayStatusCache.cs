namespace web.Services.Sms;

// Deler seneste kendte gateway-status (online/offline, saldo) mellem
// SmsStatusUpdateService (som opdaterer den periodisk i baggrunden) og
// AdminController (som læser den ved sidevisning og ved klientens polling).
// Ren in-memory reference-swap — ingen låsning nødvendig da tildeling af en
// reference er atomisk.
public sealed class SmsGatewayStatusSnapshot
{
    public bool Online { get; init; }
    public string? HealthStatus { get; init; }
    public DateTime? HealthTimestamp { get; init; }
    public string? ErrorMessage { get; init; }
    public decimal? Balance { get; init; }
    public DateTime? BalanceUpdatedAt { get; init; }

    // Prisen for én sms lige nu — bruges til at afgøre om saldoen rækker til
    // at slå sms-flowet i dashboardet til (se DashboardController.SetSmsFlowEnabled
    // og SmsStatusUpdateService's automatiske sluk ved tom saldo).
    public decimal? SmsPriceDkk { get; init; }
}

public interface ISmsGatewayStatusCache
{
    SmsGatewayStatusSnapshot Current { get; }
    void Update(SmsGatewayStatusSnapshot snapshot);
}

public sealed class SmsGatewayStatusCache : ISmsGatewayStatusCache
{
    private SmsGatewayStatusSnapshot _current = new();

    public SmsGatewayStatusSnapshot Current => _current;

    public void Update(SmsGatewayStatusSnapshot snapshot) => _current = snapshot;
}
