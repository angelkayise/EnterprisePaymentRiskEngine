# Enterprise Payment & Real-Time Risk Engine

A high-integrity **.NET 10 Web API** and **MySQL** transactional payment processor engineered with real-time fraud & velocity scoring, **POPIA-compliant structured logging (Serilog)**, pessimistic row-level database locking, and automated **xUnit** test coverage.

Built to mirror enterprise banking and fintech payment-processing standards (atomic ACID ledger guarantees, sub-40ms risk evaluation, and end-to-end request correlation tracing).

---

## Key Engineering Highlights

- **ACID-Compliant Concurrency (`SELECT ... FOR UPDATE`):** Executes all balance checks, debits, credits, and audit writes inside explicit MySQL transactions with row-level locks to eliminate race conditions and double-spending under simultaneous requests.
- **Real-Time Fraud & Velocity Scoring:** Evaluates every transfer against dynamic monetary thresholds (`>= R15,000` elevated, `>= R50,000` high-value) and 60-second transfer frequency spikes (`>= 3 transfers/min`) backed by a composite index (`idx_from_created`), routing transactions to `APPROVED`, `FLAGGED_REVIEW`, or `REJECTED`.
- **POPIA Compliance & Observability:** Assigns a unique `CorrelationId` (GUID) to every transfer, tracks execution latency in milliseconds, and automatically masks sensitive account identifiers (`ACC-1001` → `****1001`) in console and rolling JSON logs.
- **Automated Verification (`xUnit`):** Includes a dedicated unit test suite validating risk scoring thresholds, velocity spikes, combined rejection states, and POPIA account masking.

---

## System Architecture

```text
Client (.http / REST)
       │
       ▼
┌──────────────────────────────────────────────────────────┐
│  PaymentRisk.Api (.NET 10 Minimal API)                   │
│  ├── CorrelationId & Stopwatch Latency Middleware        │
│  ├── RiskEvaluationService (Threshold & Velocity Rules)  │
│  └── Serilog Structured Logger (POPIA Account Masking)   │
└──────────────────────────────┬───────────────────────────┘
                               │ MySqlConnector (ACID Tx)
                               ▼
┌──────────────────────────────────────────────────────────┐
│  MySQL 8.0 (payment_risk_db)                             │
│  ├── accounts     (Row-Level Locking: FOR UPDATE)        │
│  └── transactions (Audit Trail + idx_from_created Index) │
└──────────────────────────────────────────────────────────┘
```

---

## Verified Local Benchmarks

| Test Scenario | Input | Risk Score | Decision | Measured Latency | Balance Impact |
| :--- | :--- | :---: | :--- | :---: | :--- |
| **Database Health Check** | `GET /health` | — | `Healthy` | `334.34 ms` *(cold start)* | None |
| **Standard Transfer** | `R5,000.00` (`ACC-1001` → `ACC-1002`) | `0` | `APPROVED` | `266.68 ms` | Deducted cleanly (`R95,000.00`) |
| **High-Value Risk Flag** | `R60,000.00` (`ACC-1001` → `ACC-1002`) | `60` | `FLAGGED_REVIEW` | **`37.50 ms`** | Protected (`R95,000.00` intact) |

---

## Tech Stack

- **Backend:** C#, .NET 10 Minimal APIs
- **Database:** MySQL 8.0 (`MySqlConnector`, parameterized queries, composite indexing, pessimistic row locking)
- **Observability:** Serilog (`Serilog.AspNetCore` console + daily rolling JSON file sinks)
- **Testing:** xUnit (`PaymentRisk.Tests`) & Visual Studio `.http` integration suite

---

## Project Structure

```text
EnterprisePaymentRiskEngine/
├── database/
│   └── schema.sql                  # Normalized MySQL schema, composite indexes & seed data
├── PaymentRisk.Api/
│   ├── Models/
│   │   └── TransferModels.cs       # Immutable request/response DTO records
│   ├── Services/
│   │   └── RiskEvaluationService.cs # Rule-based risk scoring & POPIA masking
│   ├── Program.cs                  # Transactional API endpoints & Serilog pipeline
│   └── PaymentRisk.Api.http        # Integration test suite
├── PaymentRisk.Tests/
│   └── RiskEvaluationTests.cs      # Automated xUnit unit tests (5/5 passing)
└── README.md
```

---

## Quick Start

1. **Initialize the Database:**
   Run `database/schema.sql` in MySQL Workbench to create `payment_risk_db` and seed test accounts (`ACC-1001`, `ACC-1002`).
2. **Configure Connection String:**
   Update `connString` in `PaymentRisk.Api/Program.cs` with your local MySQL credentials.
3. **Run Automated Unit Tests:**
   ```bash
   dotnet test
   ```
4. **Launch the API & Send Test Requests:**
   ```bash
   dotnet run --project PaymentRisk.Api
   ```
   Open `PaymentRisk.Api/PaymentRisk.Api.http` in Visual Studio and execute the `/health` and `/api/transfers` requests.
