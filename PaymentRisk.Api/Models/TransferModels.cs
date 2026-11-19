namespace PaymentRisk.Api.Models;

public record TransferRequest(
    string FromAccountNumber,
    string ToAccountNumber,
    decimal Amount
);

public record TransferResult(
    string CorrelationId,
    string Status,
    int RiskScore,
    string? RiskReason,
    decimal? RemainingBalance
);
