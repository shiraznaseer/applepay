-- =============================================
-- Unipal Payment Integration Database Schema
-- =============================================

-- Create Unipal Payments Table
CREATE TABLE [dbo].[UnipalPayments] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [PaymentId] NVARCHAR(100) NOT NULL,
    [OrderReferenceId] NVARCHAR(100) NULL,
    [Status] NVARCHAR(50) NOT NULL,
    [Amount] DECIMAL(18,2) NULL,
    [Currency] NVARCHAR(10) NULL,
    [RawResponse] NVARCHAR(MAX) NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_UnipalPayments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UK_UnipalPayments_PaymentId] UNIQUE NONCLUSTERED ([PaymentId] ASC)
);

-- Create Index for OrderReferenceId
CREATE NONCLUSTERED INDEX [IX_UnipalPayments_OrderReferenceId] ON [dbo].[UnipalPayments] ([OrderReferenceId] ASC);

-- Create Unipal Webhook Events Table
CREATE TABLE [dbo].[UnipalWebhookEvents] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [PaymentId] NVARCHAR(100) NOT NULL,
    [EventType] NVARCHAR(100) NOT NULL,
    [Status] NVARCHAR(50) NOT NULL,
    [RawBody] NVARCHAR(MAX) NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_UnipalWebhookEvents] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- Create Index for PaymentId in WebhookEvents
CREATE NONCLUSTERED INDEX [IX_UnipalWebhookEvents_PaymentId] ON [dbo].[UnipalWebhookEvents] ([PaymentId] ASC);

-- =============================================
-- Optional: Add Foreign Key Relationship
-- =============================================
-- Uncomment the following if you want to enforce referential integrity
-- ALTER TABLE [dbo].[UnipalWebhookEvents] 
-- ADD CONSTRAINT [FK_UnipalWebhookEvents_UnipalPayments_PaymentId] 
-- FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[UnipalPayments] ([PaymentId]) 
-- ON DELETE CASCADE;

-- =============================================
-- Sample Data (Optional - for testing)
-- =============================================
-- INSERT INTO [dbo].[UnipalPayments] 
-- ([PaymentId], [OrderReferenceId], [Status], [Amount], [Currency], [RawResponse])
-- VALUES 
-- ('unipal_test_001', 'ORDER_12345', 'created', 100.00, 'USD', '{"id":"unipal_test_001","status":"created"}');

-- INSERT INTO [dbo].[UnipalWebhookEvents] 
-- ([PaymentId], [EventType], [Status], [RawBody])
-- VALUES 
-- ('unipal_test_001', 'payment.created', 'created', '{"payment":{"id":"unipal_test_001","status":"created"},"event_type":"payment.created"}');

-- =============================================
-- Common Queries for Reference
-- =============================================

-- Get payment with all webhook events
-- SELECT p.*, e.EventType, e.Status as WebhookStatus, e.CreatedAt as WebhookCreatedAt
-- FROM [dbo].[UnipalPayments] p
-- LEFT JOIN [dbo].[UnipalWebhookEvents] e ON p.PaymentId = e.PaymentId
-- WHERE p.PaymentId = 'your-payment-id'
-- ORDER BY e.CreatedAt DESC;

-- Get recent webhook events for a payment
-- SELECT * FROM [dbo].[UnipalWebhookEvents] 
-- WHERE PaymentId = 'your-payment-id'
-- ORDER BY CreatedAt DESC;

-- Get payments by status
-- SELECT * FROM [dbo].[UnipalPayments] 
-- WHERE Status = 'authorized'
-- ORDER BY CreatedAt DESC;

-- Get payments by order reference
-- SELECT * FROM [dbo].[UnipalPayments] 
-- WHERE OrderReferenceId = 'your-order-id'
-- ORDER BY CreatedAt DESC;
