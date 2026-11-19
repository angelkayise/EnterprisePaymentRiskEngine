using System.Diagnostics;
using MySqlConnector;
using PaymentRisk.Api.Models;
using PaymentRisk.Api.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/payment-risk-.json", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<RiskEvaluationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

const string connString = "Server=localhost;Database=payment_risk_db;User=root;Password=YOUR_MYSQL_PASSWORD;";

app.MapGet("/health", async () =>
{
    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    return Results.Ok(new { Status = "Healthy", Database = "Connected", Timestamp = DateTime.UtcNow });
});

app.MapPost("/api/transfers", async (TransferRequest req, RiskEvaluationService riskEngine) =>
{
    var sw = Stopwatch.StartNew();
    var correlationId = Guid.NewGuid().ToString();

    if (req.Amount <= 0 || req.FromAccountNumber == req.ToAccountNumber)
        return Results.BadRequest(new { CorrelationId = correlationId, Error = "Invalid transfer amount or accounts." });

    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    try
    {
        int fromId = 0;
        decimal balance = 0;
        await using (var cmd = new MySqlCommand(
            "SELECT account_id, balance FROM accounts WHERE account_number = @acc FOR UPDATE", conn, tx))
        {
            cmd.Parameters.AddWithValue("@acc", req.FromAccountNumber);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return Results.NotFound(new { CorrelationId = correlationId, Error = "Sender account not found." });

            fromId = reader.GetInt32(0);
            balance = reader.GetDecimal(1);
        }

        int toId = 0;
        await using (var cmd = new MySqlCommand(
            "SELECT account_id FROM accounts WHERE account_number = @acc FOR UPDATE", conn, tx))
        {
            cmd.Parameters.AddWithValue("@acc", req.ToAccountNumber);
            var result = await cmd.ExecuteScalarAsync();
            if (result is null)
                return Results.NotFound(new { CorrelationId = correlationId, Error = "Recipient account not found." });
            toId = Convert.ToInt32(result);
        }

        if (balance < req.Amount)
            return Results.BadRequest(new { CorrelationId = correlationId, Error = "Insufficient funds." });

        int recentTransfers = 0;
        await using (var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM transactions WHERE from_account_id = @id AND created_at >= NOW() - INTERVAL 1 MINUTE", conn, tx))
        {
            cmd.Parameters.AddWithValue("@id", fromId);
            recentTransfers = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        var (score, status, reason) = riskEngine.Evaluate(req.Amount, recentTransfers);
        decimal? newBalance = balance;

        if (status == "APPROVED")
        {
            newBalance = balance - req.Amount;
            await using var debit = new MySqlCommand(
                "UPDATE accounts SET balance = balance - @amt WHERE account_id = @id", conn, tx);
            debit.Parameters.AddWithValue("@amt", req.Amount);
            debit.Parameters.AddWithValue("@id", fromId);
            await debit.ExecuteNonQueryAsync();

            await using var credit = new MySqlCommand(
                "UPDATE accounts SET balance = balance + @amt WHERE account_id = @id", conn, tx);
            credit.Parameters.AddWithValue("@amt", req.Amount);
            credit.Parameters.AddWithValue("@id", toId);
            await credit.ExecuteNonQueryAsync();
        }

        await using (var logTx = new MySqlCommand(
            @"INSERT INTO transactions (correlation_id, from_account_id, to_account_id, amount, risk_score, risk_flag, status)
              VALUES (@cid, @from, @to, @amt, @score, @flag, @status)", conn, tx))
        {
            logTx.Parameters.AddWithValue("@cid", correlationId);
            logTx.Parameters.AddWithValue("@from", fromId);
            logTx.Parameters.AddWithValue("@to", toId);
            logTx.Parameters.AddWithValue("@amt", req.Amount);
            logTx.Parameters.AddWithValue("@score", score);
            logTx.Parameters.AddWithValue("@flag", (object?)reason ?? DBNull.Value);
            logTx.Parameters.AddWithValue("@status", status);
            await logTx.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        sw.Stop();

        Log.Information("Transfer {CorrelationId} | {From} -> {To} | R{Amount} | Status={Status} | Score={Score} | {ElapsedMs}ms",
            correlationId,
            RiskEvaluationService.MaskAccount(req.FromAccountNumber),
            RiskEvaluationService.MaskAccount(req.ToAccountNumber),
            req.Amount, status, score, sw.ElapsedMilliseconds);

        return Results.Ok(new TransferResult(correlationId, status, score, reason, newBalance));
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        Log.Error(ex, "Transfer {CorrelationId} failed and rolled back.", correlationId);
        return Results.Problem("Transaction failed safely.");
    }
});

app.Run();
