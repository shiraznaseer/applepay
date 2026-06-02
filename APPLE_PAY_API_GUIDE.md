# Apple Pay HyperPay API Guide

## Table of Contents

1. [Overview](#overview)
2. [Session Creation](#session-creation)
3. [Payment Processing](#payment-processing)
4. [Payment Status](#payment-status)
5. [Webhook Handling](#webhook-handling)
6. [Configuration](#configuration)
7. [Error Handling](#error-handling)
8. [Testing](#testing)
9. [Code Examples](#code-examples)

---

## Overview

This guide provides step-by-step instructions for integrating Apple Pay with HyperPay payment gateway.

### Prerequisites
- Active HyperPay merchant account
- Apple Developer account with Merchant ID
- HTTPS-enabled domain
- Valid SSL certificate
- Apple Pay domain verification file hosted

### Key Components
- **Backend API**: ASP.NET Core Web API
- **Frontend Integration**: JavaScript Apple Pay SDK
- **Payment Gateway**: HyperPay server-to-server integration
- **Webhook Handling**: Real-time payment notifications

---

## Session Creation

### 1. How to Create Apple Pay Session

#### Frontend Step: Initialize Apple Pay

```javascript
class ApplePayIntegration {
    constructor() {
        this.merchantIdentifier = 'merchant.com.tamarran.nonstop';
        this.merchantName = 'Your Store Name';
        this.countryCode = 'SA';
        this.currencyCode = 'SAR';
        this.init();
    }

    async init() {
        if (!window.ApplePaySession || !ApplePaySession.canMakePayments()) {
            console.log('Apple Pay is not supported on this device');
            return;
        }

        const canMakePayments = await ApplePaySession.canMakePaymentsWithActiveCard(this.merchantIdentifier);
        if (canMakePayments) {
            this.setupApplePayButton();
        }
    }

    setupApplePayButton() {
        const button = document.getElementById('apple-pay-button');
        button.style.display = 'block';
        button.addEventListener('click', () => this.startApplePayPayment());
    }

    async startApplePayPayment() {
        try {
            const paymentRequest = this.createPaymentRequest();
            const session = new ApplePaySession(3, paymentRequest);
            
            // IMPORTANT: Handle merchant validation
            session.onvalidatemerchant = async (event) => {
                await this.validateMerchant(event);
            };
            
            session.onpaymentauthorized = (event) => {
                await this.handlePayment(event);
            };
            
            session.oncancel = () => {
                this.handlePaymentCancel();
            };
            
            // Start the session
            session.begin();
        } catch (error) {
            console.error('Apple Pay error:', error);
            this.showStatus('Apple Pay initialization failed', 'error');
        }
    }

    async validateMerchant(event) {
        try {
            // ✅ Use Apple's validationURL (NOT hardcoded)
            const validationUrl = event.validationURL;
            console.log('Apple validationUrl:', validationUrl);
            
            const response = await fetch('/api/hyperpay/applepay/session', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    validationUrl: validationUrl, // ✅ Use Apple's validationURL
                    displayName: this.merchantName,
                    domain: window.location.hostname,
                    merchantIdentifier: this.merchantIdentifier
                })
            });

            if (response.ok) {
                const merchantSession = await response.json();
                event.session.completeMerchantValidation(merchantSession);
            } else {
                const errorText = await response.text();
                console.error('Merchant validation failed:', errorText);
                throw new Error('Merchant validation failed');
            }
        } catch (error) {
            console.error('Merchant validation error:', error);
            event.session.abort();
        }
    }

    createPaymentRequest() {
        return {
            countryCode: this.countryCode,
            currencyCode: this.currencyCode,
            merchantCapabilities: ['supports3DS'],
            supportedNetworks: ['visa', 'masterCard', 'amex'],
            total: {
                label: this.merchantName,
                amount: '100.50' // Dynamic amount
            }
        };
    }
}
```

#### Backend Step: Create Session Endpoint

**API Endpoint:** `POST /api/hyperpay/applepay/session`

**Request Body:**
```json
{
  "validationUrl": "https://apple-pay-gateway.apple.com/web-api/v1/payment/session/1234567890",
  "displayName": "Your Store Name",
  "domain": "yourdomain.com",
  "merchantIdentifier": "merchant.com.tamarran.nonstop"
}
```

**Response:** Raw Apple Pay session data (JSON string)

#### curl Example:

```bash
curl -X POST https://yourdomain.com/api/hyperpay/applepay/session \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{
    "validationUrl": "https://apple-pay-gateway.apple.com/web-api/v1/payment/session/1234567890",
    "displayName": "Your Store Name",
    "domain": "yourdomain.com",
    "merchantIdentifier": "merchant.com.tamarran.nonstop"
  }'
```

---

## Payment Processing

### 2. How to Process Apple Pay Payment

#### Frontend Step: Handle Payment Authorization

```javascript
async handlePayment(event) {
    try {
        const payment = event.payment;
        console.log('Processing payment:', payment);
        
        this.showStatus('Processing payment...', 'loading');
        
        const response = await fetch('/api/hyperpay/applepay/payment', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                amount: 100.50,
                currency: this.currencyCode,
                paymentToken: {
                    version: payment.token.version,
                    data: payment.token.data,
                    signature: payment.token.signature,
                    header: {
                        ephemeralPublicKey: payment.token.header.ephemeralPublicKey,
                        publicKeyHash: payment.token.header.publicKeyHash,
                        transactionId: payment.token.header.transactionId
                    }
                },
                customerEmail: payment.shippingContact?.emailAddress,
                customerName: `${payment.shippingContact?.givenName} ${payment.shippingContact?.familyName}`.trim()
            })
        });

        const result = await response.json();

        if (result.success) {
            // ✅ Payment successful
            event.session.completePayment(ApplePaySession.STATUS_SUCCESS);
            this.showStatus(`Payment successful! Payment ID: ${result.paymentId}`, 'success');
            
            // Redirect to success page
            setTimeout(() => {
                window.location.href = `/payment/success?paymentId=${result.paymentId}`;
            }, 2000);
        } else {
            // ❌ Payment failed
            event.session.completePayment(ApplePaySession.STATUS_FAILURE);
            this.showStatus(`Payment failed: ${result.message}`, 'error');
        }
    } catch (error) {
        console.error('Payment processing failed:', error);
        event.session.completePayment(ApplePaySession.STATUS_FAILURE);
        this.showStatus('Payment processing failed', 'error');
    }
}
```

#### Backend Step: Process Payment Endpoint

**API Endpoint:** `POST /api/hyperpay/applepay/payment`

**Request Body:**
```json
{
  "amount": "100.50",
  "currency": "SAR",
  "paymentToken": {
    "version": "EC_v1",
    "data": "base64_encoded_payment_data",
    "signature": "base64_encoded_signature",
    "header": {
      "ephemeralPublicKey": "base64_encoded_public_key",
      "publicKeyHash": "base64_encoded_hash",
      "transactionId": "transaction_identifier"
    }
  },
  "customerEmail": "customer@example.com",
  "customerName": "John Doe",
  "orderId": "optional-order-id"
}
```

**Response:**
```json
{
  "success": true,
  "paymentId": "hyperpay-payment-id",
  "result": {
    "code": "000.100.110",
    "description": "Request successfully processed"
  },
  "merchantTransactionId": "TXN-123456789",
  "timestamp": "2024-01-01T12:00:00Z"
}
```

#### curl Example:

```bash
curl -X POST https://yourdomain.com/api/hyperpay/applepay/payment \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{
    "amount": "100.50",
    "currency": "SAR",
    "paymentToken": {
      "version": "EC_v1",
      "data": "base64_encoded_payment_data",
      "signature": "base64_encoded_signature",
      "header": {
        "ephemeralPublicKey": "base64_encoded_public_key",
        "publicKeyHash": "base64_encoded_hash",
        "transactionId": "transaction_identifier"
      }
    },
    "customerEmail": "customer@example.com",
    "customerName": "John Doe"
  }'
```

---

## Payment Status

### 3. How to Check Payment Status

#### Frontend Step: Check Status

```javascript
async function getPaymentStatus(paymentId) {
    try {
        const response = await fetch(`/api/hyperpay/payment/${paymentId}/status`);
        const result = await response.json();
        
        if (result.success) {
            console.log('Payment status:', result.payment);
            return result.payment;
        } else {
            console.error('Failed to get payment status');
            return null;
        }
    } catch (error) {
        console.error('Error checking payment status:', error);
        return null;
    }
}
```

#### Backend Step: Get Status Endpoint

**API Endpoint:** `GET /api/hyperpay/payment/{paymentId}/status`

**Response:**
```json
{
  "success": true,
  "payment": {
    "id": "payment-id",
    "paymentType": "DB",
    "paymentBrand": "APPLEPAY",
    "amount": "100.50",
    "currency": "SAR",
    "result": {
      "code": "000.100.110",
      "description": "Request successfully processed"
    },
    "card": {
      "bin": "411111",
      "last4Digits": "1111",
      "holder": "John Doe",
      "expiryMonth": "12",
      "expiryYear": "2025"
    },
    "threeDSecure": {
      "verificationId": "3ds-verification-id",
      "eci": "05"
    }
  }
}
```

#### curl Example:

```bash
curl -X GET https://yourdomain.com/api/hyperpay/payment/123456789/status \
  -H "Accept: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

---

## Webhook Handling

### 4. How to Handle Webhooks

#### Backend Step: Webhook Endpoint

**API Endpoint:** `POST /api/hyperpay/webhook`

**Request Body:**
```json
{
  "payload": {
    "id": "payment-id",
    "type": "PAYMENT",
    "result": {
      "code": "000.100.110",
      "description": "Successfully processed"
    },
    "amount": "100.50",
    "currency": "SAR",
    "paymentBrand": "APPLEPAY"
  }
}
```

**Response:**
```json
{
  "success": true,
  "message": "Webhook processed successfully"
}
```

#### Webhook Handler Example:

```javascript
app.post('/api/hyperpay/webhook', express.raw({ type: 'application/json' }), (req, res) => {
    try {
        const webhookData = JSON.parse(req.body.toString());
        
        console.log('Received webhook:', webhookData);
        
        // Handle different webhook types
        switch (webhookData.payload.type) {
            case 'PAYMENT':
                await handlePaymentWebhook(webhookData.payload);
                break;
            case 'REGISTRATION':
                await handleRegistrationWebhook(webhookData.payload);
                break;
            default:
                console.log('Unknown webhook type:', webhookData.payload.type);
        }
        
        res.json({ status: 'received' });
    } catch (error) {
        console.error('Webhook error:', error);
        res.status(500).json({ 
            success: false, 
            message: 'Webhook processing failed',
            error: error.message 
        });
    }
});

async function handlePaymentWebhook(payload) {
    // TODO: Update payment status in your database
    // 1. Find payment by ID
    // 2. Update status based on result code
    // 3. Send notifications to customer
    // 4. Trigger any business logic
    
    if (payload.result.code.startsWith('000.')) {
        console.log('Payment successful:', payload.id);
        // Handle successful payment
    } else {
        console.log('Payment failed:', payload.id, payload.result.code);
        // Handle failed payment
    }
}
```

---

## Configuration

### 5. How to Configure Your Application

#### appsettings.json Configuration

```json
{
  "HyperPay": {
    "BaseUrl": "https://test.oppwa.com",
    "EntityId": "8ac7a4c99d78eb11019d863e638d08e8",
    "AccessToken": "OGFjN2E0Yzc5YmMxMTVhNTAxOWJjMTc0MDk0YTAyNjN8ckBVU1QyN3VpM216I0RDK0BmTlE=",
    "AppleMerchantId": "merchant.com.tamarran.nonstop",
    "Currency": "SAR",
    "IsTestMode": true
  }
}
```

#### Production Configuration

```json
{
  "HyperPay": {
    "BaseUrl": "https://oppwa.com",
    "EntityId": "your-production-entity-id",
    "AccessToken": "your-production-access-token",
    "AppleMerchantId": "merchant.com.tamarran.nonstop",
    "Currency": "SAR",
    "IsTestMode": false
  }
}
```

#### Environment Variables

```bash
# Test Environment
export HYPERPAY_BASE_URL=https://test.oppwa.com
export HYPERPAY_ENTITY_ID=8ac7a4c99d78eb11019d863e638d08e8
export HYPERPAY_ACCESS_TOKEN=YOUR_ACCESS_TOKEN
export HYPERPAY_MERCHANT_ID=merchant.com.tamarran.nonstop

# Production Environment
export HYPERPAY_BASE_URL=https://oppwa.com
export HYPERPAY_ENTITY_ID=your-production-entity-id
export HYPERPAY_ACCESS_TOKEN=your-production-access-token
export HYPERPAY_MERCHANT_ID=merchant.com.tamarran.nonstop
```

---

## Error Handling

### 6. Common Error Codes and Solutions

#### Success Codes
- `000.200.100` - Successfully created checkout
- `000.100.110` - Successfully processed
- `000.000.000` - Successfully processed

#### Error Codes
- `800.400.100` - Invalid entity ID
- `800.400.102` - Invalid access token
- `900.400.300` - Invalid Apple Pay token
- `800.100.300` - Risk management decline

#### Error Handling Patterns

```javascript
// Frontend error handling
try {
    const result = await processPayment(paymentToken);
    if (result.success) {
        // Handle success
        showSuccessMessage(result.paymentId);
    } else {
        // Handle error
        showErrorMessage(result.message);
    }
} catch (error) {
    console.error('Payment error:', error);
    showErrorMessage('Payment processing failed');
}
```

```csharp
// Backend error handling
try {
    var result = await _hyperPayService.ProcessPaymentAsync(request);
    return Ok(result);
} catch (Exception ex) {
    _logger.LogError(ex, "Error processing Apple Pay payment");
    return StatusCode(500, new { 
        success = false, 
        message = "Failed to process Apple Pay payment",
        error = ex.Message 
    });
}
```

---

## Testing

### 7. How to Test Your Integration

#### Test Environment Setup

1. **Use Test Credentials**: Provided test entity ID and access token
2. **Test Domain**: Ensure HTTPS and domain verification
3. **Test Devices**: Use Safari on iPhone/Mac with Apple Pay

#### Test Scenarios

```javascript
// Test successful payment
const testPayment = {
    amount: 1.00,
    currency: 'SAR',
    // Use test Apple Pay token
    paymentToken: getTestApplePayToken()
};

// Test failed payment scenarios
const testScenarios = [
    { name: 'Invalid token', token: 'invalid-token' },
    { name: 'Network error', simulateNetworkError: true },
    { name: 'Insufficient funds', simulateInsufficientFunds: true }
];
```

#### Test Cards

| Brand | Card Number | CVV | Expiry | Result |
|-------|-------------|-----|--------|--------|
| VISA | 4111111111111111 | 123 | 12/2025 | Success |
| MasterCard | 5555555555554444 | 123 | 12/2025 | Success |
| AMEX | 378282246310005 | 1234 | 12/2025 | Success |
| Mada | 4000000000000002 | 123 | 12/2025 | Success |

#### Debug Steps

1. **Open browser console** and check for Apple Pay availability
2. **Verify validationUrl** is correctly passed from Apple event
3. **Check API responses** in browser network tab
4. **Test error scenarios** with different conditions
5. **Verify webhook delivery** for payment status updates

---

## Code Examples

### 8. Complete HTML Implementation

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Apple Pay Integration</title>
    <script src="https://applepay.cdn-apple.com/jsapi/v1/apple-pay-sdk.js"></script>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f5f5f7;
        }
        .container {
            max-width: 600px;
            margin: 0 auto;
            background: white;
            padding: 30px;
            border-radius: 12px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.1);
        }
        .apple-pay-button {
            -webkit-appearance: -apple-pay-button;
            -apple-pay-button-type: plain;
            -apple-pay-button-style: black;
            height: 48px;
            width: 100%;
            max-width: 300px;
            margin: 20px 0;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            transition: opacity 0.2s;
        }
        .apple-pay-button:hover {
            opacity: 0.8;
        }
        .status {
            padding: 15px;
            margin: 15px 0;
            border-radius: 8px;
            font-weight: 500;
        }
        .success {
            background-color: #d4edda;
            color: #155724;
            border: 1px solid #c3e6cb;
        }
        .error {
            background-color: #f8d7da;
            color: #721c24;
            border: 1px solid #f5c6cb;
        }
        .loading {
            background-color: #fff3cd;
            color: #856404;
            border: 1px solid #ffeaa7;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>Apple Pay Integration</h1>
        <div class="product-info">
            <h2>Premium Product</h2>
            <p>Amount: SAR 100.50</p>
        </div>
        
        <button id="apple-pay-button" class="apple-pay-button" style="display: none;">
            Pay with Apple Pay
        </button>
        
        <div id="status" class="status" style="display: none;"></div>
    </div>

    <script>
        class ApplePayIntegration {
            constructor() {
                this.merchantIdentifier = 'merchant.com.tamarran.nonstop';
                this.merchantName = 'Your Store Name';
                this.countryCode = 'SA';
                this.currencyCode = 'SAR';
                this.amount = 100.50;
                this.init();
            }

            async init() {
                if (!window.ApplePaySession) {
                    this.showStatus('Apple Pay is not supported on this device', 'error');
                    return;
                }

                try {
                    const canMakePayments = await ApplePaySession.canMakePaymentsWithActiveCard(this.merchantIdentifier);
                    if (canMakePayments) {
                        this.setupApplePayButton();
                    } else {
                        this.showStatus('No active cards available for this merchant', 'info');
                    }
                } catch (error) {
                    console.error('Error checking Apple Pay availability:', error);
                    this.showStatus('Error initializing Apple Pay', 'error');
                }
            }

            setupApplePayButton() {
                const button = document.getElementById('apple-pay-button');
                button.style.display = 'block';
                button.addEventListener('click', () => this.startApplePayPayment());
            }

            async startApplePayPayment() {
                try {
                    this.showStatus('Processing payment...', 'loading');
                    
                    const paymentRequest = this.createPaymentRequest();
                    const session = new ApplePaySession(3, paymentRequest);
                    
                    session.onvalidatemerchant = async (event) => {
                        await this.validateMerchant(event);
                    };
                    
                    session.onpaymentauthorized = async (event) => {
                        await this.handlePayment(event);
                    };
                    
                    session.oncancel = () => {
                        this.handlePaymentCancel();
                    };
                    
                    session.begin();
                } catch (error) {
                    console.error('Apple Pay error:', error);
                    this.showStatus('Apple Pay initialization failed', 'error');
                }
            }

            createPaymentRequest() {
                return {
                    countryCode: this.countryCode,
                    currencyCode: this.currencyCode,
                    merchantCapabilities: ['supports3DS'],
                    supportedNetworks: ['visa', 'masterCard', 'amex'],
                    total: {
                        label: this.merchantName,
                        amount: this.amount.toString()
                    }
                };
            }

            async validateMerchant(event) {
                try {
                    const validationUrl = event.validationURL;
                    console.log('Apple validationUrl:', validationUrl);
                    
                    const response = await fetch('/api/hyperpay/applepay/session', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                        },
                        body: JSON.stringify({
                            validationUrl: validationUrl,
                            displayName: this.merchantName,
                            domain: window.location.hostname,
                            merchantIdentifier: this.merchantIdentifier
                        })
                    });

                    if (!response.ok) {
                        throw new Error(`Session creation failed: ${response.status}`);
                    }

                    const merchantSession = await response.json();
                    event.session.completeMerchantValidation(merchantSession);
                    
                    console.log('Merchant validation completed');
                } catch (error) {
                    console.error('Merchant validation failed:', error);
                    event.session.abort();
                }
            }

            async handlePayment(event) {
                try {
                    const payment = event.payment;
                    console.log('Processing payment:', payment);
                    
                    const response = await fetch('/api/hyperpay/applepay/payment', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                        },
                        body: JSON.stringify({
                            amount: this.amount,
                            currency: this.currencyCode,
                            paymentToken: {
                                version: payment.token.version,
                                data: payment.token.data,
                                signature: payment.token.signature,
                                header: {
                                    ephemeralPublicKey: payment.token.header.ephemeralPublicKey,
                                    publicKeyHash: payment.token.header.publicKeyHash,
                                    transactionId: payment.token.header.transactionId
                                }
                            },
                            customerEmail: payment.shippingContact?.emailAddress,
                            customerName: `${payment.shippingContact?.givenName} ${payment.shippingContact?.familyName}`.trim()
                        })
                    });

                    const result = await response.json();

                    if (result.success) {
                        event.session.completePayment(ApplePaySession.STATUS_SUCCESS);
                        this.showStatus(`Payment successful! Payment ID: ${result.paymentId}`, 'success');
                    } else {
                        event.session.completePayment(ApplePaySession.STATUS_FAILURE);
                        this.showStatus(`Payment failed: ${result.message}`, 'error');
                    }
                } catch (error) {
                    console.error('Payment processing failed:', error);
                    event.session.completePayment(ApplePaySession.STATUS_FAILURE);
                    this.showStatus('Payment processing failed', 'error');
                }
            }

            handlePaymentCancel() {
                console.log('Payment cancelled by user');
                this.showStatus('Payment cancelled', 'info');
            }

            showStatus(message, type) {
                const statusDiv = document.getElementById('status');
                statusDiv.textContent = message;
                statusDiv.className = `status ${type}`;
                statusDiv.style.display = 'block';
            }
        }

        // Initialize when DOM is loaded
        document.addEventListener('DOMContentLoaded', () => {
            new ApplePayIntegration();
        });
    </script>
</body>
</html>
```

### 9. Complete Backend Implementation

```csharp
[ApiController]
[Route("api/[controller]")]
public class HyperPayController : ControllerBase
{
    private readonly IHyperPayService _hyperPayService;
    private readonly HyperPayOptions _options;
    private readonly ILogger<HyperPayController> _logger;

    public HyperPayController(
        IHyperPayService hyperPayService,
        IOptions<HyperPayOptions> options,
        ILogger<HyperPayController> logger)
    {
        _hyperPayService = hyperPayService;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("applepay/session")]
    public async Task<IActionResult> CreateApplePaySession([FromBody] ApplePaySessionRequest request)
    {
        try
        {
            _logger.LogInformation("Creating Apple Pay session for domain: {Domain}", request.Domain);
            
            var sessionData = await _hyperPayService.CreateApplePaySessionAsync(request);
            
            return Content(sessionData, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Apple Pay session");
            return StatusCode(500, new { 
                success = false, 
                message = "Failed to create Apple Pay session",
                error = ex.Message 
            });
        }
    }

    [HttpPost("applepay/payment")]
    public async Task<IActionResult> ProcessApplePayPayment([FromBody] ApplePayPaymentRequest request)
    {
        try
        {
            _logger.LogInformation("Processing Apple Pay payment for amount: {Amount} {Currency}", 
                request.Amount, request.Currency);
            
            var response = await _hyperPayService.ProcessApplePayPaymentAsync(request);
            
            return Ok(new { 
                success = true, 
                paymentId = response.Id,
                result = response.Result,
                merchantTransactionId = response.MerchantTransactionId,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Apple Pay payment");
            return StatusCode(500, new { 
                success = false, 
                message = "Failed to process Apple Pay payment",
                error = ex.Message 
            });
        }
    }

    [HttpGet("payment/{paymentId}/status")]
    public async Task<IActionResult> GetPaymentStatus(string paymentId)
    {
        try
        {
            var status = await _hyperPayService.GetPaymentStatusAsync(paymentId);
            
            return Ok(new { 
                success = true, 
                payment = status,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment status for ID: {PaymentId}", paymentId);
            return StatusCode(500, new { 
                success = false, 
                message = "Failed to get payment status",
                error = ex.Message 
            });
        }
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            
            _logger.LogInformation("Received HyperPay webhook: {WebhookData}", requestBody);
            
            // TODO: Parse webhook data and update payment status in database
            // This should handle payment success, failure, and other status updates

            return Ok(new { success = true, message = "Webhook processed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing HyperPay webhook");
            return StatusCode(500, new { 
                success = false, 
                message = "Failed to process webhook",
                error = ex.Message 
            });
        }
    }
}
```

---

## Quick Reference

### API Endpoints Summary

| Endpoint | Method | Purpose | Request | Response |
|---------|--------|---------|---------|----------|
| `/api/hyperpay/applepay/session` | POST | Create Apple Pay session | Apple Pay session data |
| `/api/hyperpay/applepay/payment` | POST | Process Apple Pay payment | Payment result |
| `/api/hyperpay/payment/{id}/status` | GET | Check payment status | Payment details |
| `/api/hyperpay/webhook` | POST | Handle webhooks | Success confirmation |

### Configuration Parameters

| Parameter | Description | Example |
|----------|-------------|---------|
| `BaseUrl` | HyperPay API URL | `https://test.oppwa.com` |
| `EntityId` | Merchant entity ID | `8ac7a4c99d78eb11019d863e638d08e8` |
| `AccessToken` | Bearer token | `YOUR_ACCESS_TOKEN` |
| `AppleMerchantId` | Apple merchant ID | `merchant.com.tamarran.nonstop` |
| `Currency` | Default currency | `SAR` |
| `IsTestMode` | Test environment flag | `true` |

### Error Codes Reference

| Code | Description | Action |
|------|-------------|--------|
| `000.200.100` | Successfully created checkout | Continue with payment |
| `000.100.110` | Successfully processed | Payment complete |
| `800.400.100` | Invalid entity ID | Check configuration |
| `800.400.102` | Invalid access token | Check authentication |
| `900.400.300` | Invalid Apple Pay token | Check token format |
| `800.100.300` | Risk management decline | Check payment data |

### Testing URLs

| Environment | Base URL | Domain Verification |
|------------|----------|------------------|
| Test | `https://test.oppwa.com` | `https://yourdomain.com/.well-known/apple-developer-merchantid-domain-association` |
| Production | `https://oppwa.com` | Same as test |

---

## Support Resources

### Technical Support
- **HyperPay Documentation**: https://hyperpay.docs.oppwa.com/
- **HyperPay API Reference**: https://hyperpay.docs.oppwa.com/reference/parameters
- **Apple Developer Portal**: https://developer.apple.com/apple-pay/
- **Apple Pay Documentation**: https://developer.apple.com/documentation/apple-pay-on-the-web/

### Contact Information
- **HyperPay Support**: support@hyperpay.com
- **Apple Developer Support**: Contact through Apple Developer portal
- **Emergency Support**: Available 24/7 for production issues

---

*This guide provides comprehensive instructions for integrating Apple Pay with HyperPay payment gateway. For additional assistance, refer to the official documentation and support channels.*
