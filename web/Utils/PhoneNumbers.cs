namespace web.Utils;

// SMS-gatewayen kan kun sende til danske numre, og forventer dem ALTID som et
// lokalt 8-cifret nummer UDEN landekode (fx "25709075" — aldrig "4525709075"
// eller "+4525709075"). Frivillige kan have nummeret gemt både med og uden
// landekode i vores egen database, så ethvert nummer SKAL normaliseres via
// denne klasse før det bruges til at sammenligne med eller sendes til
// gatewayen (opret/rediger abonnementsliste, match mod abonnementslister,
// afsendelse af sms). Numre der ikke normaliserer til præcis 8 cifre er ikke
// gyldige danske numre og må aldrig sendes til gatewayen.
public static class PhoneNumbers
{
    public static bool TryNormalizeDanish(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("45") && digits.Length == 10)
            digits = digits[2..];

        if (digits.Length != 8)
            return false;

        normalized = digits;
        return true;
    }

    public static string? NormalizeDanishOrNull(string? value)
        => TryNormalizeDanish(value, out var normalized) ? normalized : null;
}
