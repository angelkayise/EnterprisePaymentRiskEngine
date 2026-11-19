using PaymentRisk.Api.Services;

namespace PaymentRisk.Tests;

public class RiskEvaluationTests
{
    private readonly RiskEvaluationService _service = new();

    [Fact]
    public void Evaluate_NormalTransfer_ReturnsApproved()
    {
        var (score, status, reason) = _service.Evaluate(5000m, 0);

        Assert.Equal(0, score);
        Assert.Equal("APPROVED", status);
        Assert.Null(reason);
    }

    [Fact]
    public void Evaluate_HighValueTransfer_ReturnsFlaggedReview()
    {
        var (score, status, reason) = _service.Evaluate(60000m, 0);

        Assert.Equal(60, score);
        Assert.Equal("FLAGGED_REVIEW", status);
        Assert.Contains("High-value transfer", reason);
    }

    [Fact]
    public void Evaluate_VelocitySpike_ReturnsFlaggedReview()
    {
        var (score, status, reason) = _service.Evaluate(1000m, 3);

        Assert.Equal(50, score);
        Assert.Equal("FLAGGED_REVIEW", status);
        Assert.Contains("Velocity spike", reason);
    }

    [Fact]
    public void Evaluate_HighValueAndVelocitySpike_ReturnsRejected()
    {
        var (score, status, reason) = _service.Evaluate(60000m, 4);

        Assert.Equal(110, score);
        Assert.Equal("REJECTED", status);
    }

    [Fact]
    public void MaskAccount_ValidAccountNumber_MasksSensitivePrefix()
    {
        var masked = RiskEvaluationService.MaskAccount("ACC-1001");

        Assert.Equal("****1001", masked);
    }
}
