# 🧪 ApplePay WebSocket Testing Guide

## 🚀 Quick Start

### 1. Run the Application
```bash
dotnet run
```
The app will start on `https://localhost:7123`

### 2. Access Points
- **Swagger UI**: `https://localhost:7123` (root)
- **WebSocket Test Page**: `https://localhost:7123/websocket-test.html`
- **Admin Dashboard**: `https://localhost:7123/websocket-dashboard` (requires Admin token)

## 🔐 JWT Token Generation

### Method 1: Via Auth API (Recommended for External Websites)

#### Generate Token for External Users
```bash
curl -X POST "https://localhost:7123/api/auth/generate-token" \
-H "Content-Type: application/json" \
-d '{
  "userId": "external-user-123",
  "role": "User"
}'
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600,
  "userId": "external-user-123",
  "role": "User"
}
```

#### Login with Credentials
```bash
curl -X POST "https://localhost:7123/api/auth/login" \
-H "Content-Type: application/json" \
-d '{
  "username": "admin",
  "password": "admin123"
}'
```

### Method 2: Use Pre-generated Test Token
```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhZG1pbiIsInJvbGUiOiJBZG1pbiIsImlhdCI6MTcwMTIzNDU2NywiZXhwIjoxNzAxMjM4MTY3fQ.3bYxTmX2sL7iQh8jK9N5p6rR7vY8tZ2wX4vY7k8m9nQ
```

## 📡 WebSocket Connection Methods

### Method 1: Query Parameter (Easiest for External Sites)
```javascript
const token = "YOUR_JWT_TOKEN";
const ws = new WebSocket(`wss://localhost:7123/ws?token=${token}`);
```

### Method 2: Authorization Header
```javascript
// Note: Browser WebSocket API doesn't support custom headers directly
// Use query parameter method for browser clients
```

### Method 3: Cookie (for same-origin)
```javascript
document.cookie = `access_token=${token}; path=/; secure; samesite=strict`;
const ws = new WebSocket('wss://localhost:7123/ws');
```

## 🧪 Testing Scenarios

### Scenario 1: External Website Integration

1. **Generate Token** for your external user
2. **Connect WebSocket** using the token
3. **Subscribe to Events**
4. **Receive Payment Updates**

```javascript
// Example for external website
async function initWebSocket() {
    // 1. Get token from your auth server
    const response = await fetch('https://your-api.com/api/auth/generate-token', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId: 'customer-123', role: 'User' })
    });
    const { token } = await response.json();
    
    // 2. Connect WebSocket
    const ws = new WebSocket(`wss://your-payment-api.com/ws?token=${token}`);
    
    ws.onopen = () => {
        console.log('Connected!');
        ws.send(JSON.stringify({ type: 'subscribe', events: ['payment.updated'] }));
    };
    
    ws.onmessage = (event) => {
        const payment = JSON.parse(event.data);
        console.log('Payment update:', payment);
        // Update your UI
        updatePaymentStatus(payment);
    };
}
```

### Scenario 2: Swagger Testing

1. **Open Swagger**: `https://localhost:7123`
2. **Click "Authorize"** button
3. **Enter**: `Bearer YOUR_JWT_TOKEN`
4. **Test endpoints**:
   - `GET /api/websocket/health`
   - `POST /api/websocket/test-notification`
   - `GET /api/websocket/stats`

### Scenario 3: Using Test Page

1. **Visit**: `https://localhost:7123/websocket-test.html`
2. **Enter User ID** (e.g., "external-user-123")
3. **Click "Generate Token"**
4. **Click "Connect"**
5. **Send test messages**

## 📊 Available Endpoints

### Authentication
- `POST /api/auth/login` - Login with credentials
- `POST /api/auth/generate-token` - Generate token for external users
- `GET /api/auth/validate` - Validate existing token

### WebSocket
- `WS /ws` - WebSocket connection endpoint

### WebSocket Management
- `GET /api/websocket/health` - Service health check
- `GET /api/websocket/stats` - Connection statistics
- `GET /api/websocket/connections` - Connected users list
- `POST /api/websocket/test-notification` - Send test payment update
- `GET /api/websocket/is-user-connected/{userId}` - Check user connection

## 🔧 CORS Configuration

The API is configured to accept requests from any origin with credentials:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials(); // Important for WebSocket cookies
    });
});
```

## 🌐 External Integration Examples

### React Frontend
```javascript
import React, { useEffect, useState } from 'react';

function PaymentStatus({ paymentId }) {
    const [status, setStatus] = useState('loading');
    const [ws, setWs] = useState(null);

    useEffect(() => {
        async function connect() {
            // Get token from your auth system
            const token = await getAuthToken();
            
            const websocket = new WebSocket(`wss://api.yoursite.com/ws?token=${token}`);
            
            websocket.onopen = () => {
                websocket.send(JSON.stringify({ 
                    type: 'subscribe', 
                    events: ['payment.updated'] 
                }));
            };
            
            websocket.onmessage = (event) => {
                const data = JSON.parse(event.data);
                if (data.paymentId === paymentId) {
                    setStatus(data.status);
                }
            };
            
            setWs(websocket);
        }
        
        connect();
        
        return () => ws?.close();
    }, [paymentId]);

    return <div>Payment Status: {status}</div>;
}
```

### Vanilla JavaScript
```html
<script>
class PaymentWebSocketClient {
    constructor(apiUrl, userId) {
        this.apiUrl = apiUrl;
        this.userId = userId;
        this.ws = null;
    }
    
    async connect() {
        // Get token
        const response = await fetch(`${this.apiUrl}/api/auth/generate-token`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userId: this.userId })
        });
        const { token } = await response.json();
        
        // Connect WebSocket
        this.ws = new WebSocket(`${this.apiUrl.replace('http', 'ws')}/ws?token=${token}`);
        
        this.ws.onopen = () => {
            console.log('Connected to payment updates');
            this.ws.send(JSON.stringify({ type: 'subscribe', events: ['payment.updated'] }));
        };
        
        this.ws.onmessage = (event) => {
            const payment = JSON.parse(event.data);
            this.handlePaymentUpdate(payment);
        };
    }
    
    handlePaymentUpdate(payment) {
        console.log('Payment updated:', payment);
        // Update your UI here
    }
}

// Usage
const client = new PaymentWebSocketClient('https://localhost:7123', 'user-123');
client.connect();
</script>
```

## 🔍 Troubleshooting

### Common Issues

#### "401 Unauthorized" on WebSocket
- **Cause**: Invalid or expired JWT token
- **Fix**: Generate a fresh token and check the secret key

#### "403 Forbidden" 
- **Cause**: Rate limiting exceeded
- **Fix**: Wait for ban to expire or check rate limit settings

#### CORS Errors
- **Cause**: External domain not allowed
- **Fix**: Ensure CORS is properly configured with `AllowCredentials()`

#### Connection Drops
- **Cause**: Network issues or server restart
- **Fix**: Implement auto-reconnection in client

### Debug Steps

1. **Check Token Validity**:
   ```bash
   curl -H "Authorization: Bearer YOUR_TOKEN" \
        https://localhost:7123/api/auth/validate
   ```

2. **Test WebSocket Health**:
   ```bash
   curl https://localhost:7123/api/websocket/health
   ```

3. **Monitor Connections**:
   Visit `/websocket-dashboard` with Admin token

## 🚀 Production Deployment

### Environment Variables
```bash
WEBSOCKET_AUTH_SECRET_KEY=your-production-secret-key
WEBSOCKET_AUTH_ISSUER=YourAPI
WEBSOCKET_AUTH_AUDIENCE=YourClients
```

### Security Considerations
- ✅ Use HTTPS/WSS in production
- ✅ Set short token expiration (1 hour)
- ✅ Implement rate limiting
- ✅ Monitor connection attempts
- ✅ Use secure cookie flags

### Load Balancing
- WebSocket connections need sticky sessions
- Consider Redis for distributed connection state
- Monitor connection count per instance

## 📞 Support

For issues:
1. Check server logs
2. Verify token format
3. Test with the test page first
4. Monitor the admin dashboard
