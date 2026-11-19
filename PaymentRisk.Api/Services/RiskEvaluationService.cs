namespace PaymentRisk.Api.Services;

public class RiskEvaluationService
{
    public (int Score, string Status, string? Reason) Evaluate(decimal amount, int transfersInLastMinute)
    {
        int score = 0;
        var reasons = new List<string>();

        if (amount >= 50000m)
        {
            score += 60;
            reasons.Add("High-value transfer (>= R50,000)");
        }
        else if (amount >= 15000m)
        {
            score += 25;
            reasons.Add("Elevated transfer value");
        }

        if (transfersInLastMinute >= 3)
        {
            score += 50;
            reasons.Add($"Velocity spike ({transfersInLastMinute} transfers in 60s)");
        }

        string status = score >= 75 ? "REJECTED"
                      : score >= 50 ? "FLAGGED_REVIEW"
                      : "APPROVED";

        return (score, status, reasons.Count > 0 ? string.Join("; ", reasons) : null);
    }

    public static string MaskAccount(string accountNumber) =>
        accountNumber.Length <= 4 ? "****" : $"****{accountNumber[^4..]}";
}
