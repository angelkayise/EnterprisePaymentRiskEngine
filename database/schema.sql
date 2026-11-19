CREATE DATABASE IF NOT EXISTS payment_risk_db;
USE payment_risk_db;

CREATE TABLE accounts (
    account_id INT AUTO_INCREMENT PRIMARY KEY,
    account_number VARCHAR(20) NOT NULL UNIQUE,
    holder_name VARCHAR(100) NOT NULL,
    balance DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    is_frozen BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE transactions (
    transaction_id INT AUTO_INCREMENT PRIMARY KEY,
    correlation_id VARCHAR(36) NOT NULL,
    from_account_id INT NOT NULL,
    to_account_id INT NOT NULL,
    amount DECIMAL(18, 2) NOT NULL,
    risk_score INT NOT NULL DEFAULT 0,
    risk_flag VARCHAR(100) NULL,
    status ENUM('APPROVED', 'FLAGGED_REVIEW', 'REJECTED') NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_from_created (from_account_id, created_at),
    FOREIGN KEY (from_account_id) REFERENCES accounts(account_id),
    FOREIGN KEY (to_account_id) REFERENCES accounts(account_id)
);

INSERT INTO accounts (account_number, holder_name, balance) VALUES
('ACC-1001', 'N. A. Mirindi', 100000.00),
('ACC-1002', 'Investec Treasury Vault', 500000.00);
