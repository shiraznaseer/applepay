# Tabby Webhook Testing Guide

## Issue Fixed
The main issue was that payment capture was not working because:
1. Status comparison was case-sensitive (comparing "authorized" vs "AUTHORIZED")
2. Payment verification was calling the wrong method that also saved to database
3. No proper database update after successful capture

## Changes Made

### 1. Fixed Status Comparison
- Changed webhook status check from `"authorized"` to `"AUTHORIZED"`
- Used `StringComparison.OrdinalIgnoreCase` for robust comparison

### 2. Added Payment Verification Method
- Created `VerifyPaymentAsync()` method for pure API calls without database operations
- Updated webhook to use this method for payment verification before capture

### 3. Enhanced Capture Process
- Added database status update after successful capture
- Improved error handling and logging
- Added `UpdatePaymentStatusInDatabaseAsync()` method

### 4. Added Test Endpoint
- Created `/api/tabby/test-webhook` endpoint for testing
- Added `TabbyWebhookTest` class with sample payload generation

## How to Test

### Method 1: Using Test Endpoint
```bash
POST /api/tabby/test-webhook
Content-Type: application/json

# No body needed - uses auto-generated test payload
```

### Method 2: Manual Webhook Test
```bash
POST /api/tabby/webhook
Content-Type: application/json
X-Tabby-Signature: your-secret-key

{
  "id": "test_payment_12345678",
  "status": "AUTHORIZED",
  "amount": "100.00",
  "currency": "AED",
  "payment": {
    "id": "test_payment_12345678",
    "status": "AUTHORIZED",
    "amount": "100.00",
    "currency": "AED",
    "buyer": {
      "name": "Test User",
      "email": "test@example.com",
      "phone": "+971501234567"
    }
  },
  "order": {
    "reference_id": "TEST_ORDER_123456"
  },
  "created_at": "2024-01-01T12:00:00Z"
}
```

## Expected Flow
1. Webhook receives AUTHORIZED status
2. System verifies payment status via Tabby API
3. If verified as AUTHORIZED, system captures payment
4. Database is updated with CAPTURED status
5. WebSocket notification is sent

## Logging
- All webhook events are logged to `Logs/tabby-webhook.log`
- Detailed logging for verification, capture, and error scenarios
- Database operation logging for troubleshooting

## Important Notes
- Capture only happens when status is "AUTHORIZED"
- Payment verification prevents capturing non-authorized payments
- Database updates ensure proper payment tracking
- All errors are logged but webhook returns success to avoid retry loops
