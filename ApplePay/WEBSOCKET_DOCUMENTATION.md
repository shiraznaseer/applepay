# 🍎 ApplePay WebSocket Integration Guide

## 🔄 Overview
This system provides real-time payment updates using WebSockets. It allows external frontends (React, Vue, mobile apps) to receive instant notifications when a payment status changes, without polling the server.

---

## 🛠️ Integration Flow



### 1. Connect to WebSocket
Establish a persistent connection using the token.

**URL:** `wss://your-domain.com/ws?token=<your-token>`

**JavaScript Example:**
```javascript
const ws = new WebSocket('wss://localhost:7274/ws?token=eyJ...');

ws.onopen = () => console.log('Connected!');
ws.onmessage = (event) => {
    const update = JSON.parse(event.data);
    console.log('Payment Update:', update);
};
```

### 2. Listen for Events
Once connected, you will automatically receive events. No subscription message is strictly required, but you can send `{"type":"ping"}` to keep the connection alive if needed.

**Event Format:**
```json
{
  "PaymentId": "pay_123",
  "Status": "authorized", 
  "Amount": 150.00,
  "OrderReferenceId": "order_456"
}
```

---

## 📡 How It Works Under the Hood

1. **User Initiates Payment:**
   - Frontend calls Tabby API to create a session.
   - User completes payment on Tabby.

2. **Tabby Webhook:**
   - Tabby sends a webhook to `POST /api/tabby/webhook`.
   - Your backend validates and processes the webhook.

3. **Real-Time Push:**
   - The backend `WebSocketNotificationService` instantly pushes the update to all connected WebSocket clients.
   - **Latency:** < 100ms (Real-time).

4. **Frontend Update:**
   - The WebSocket `onmessage` event fires on the frontend.
   - UI updates immediately to show "Payment Successful".

---

## 🧪 Testing Tools

### 1. Manual Trigger (API)
You can simulate a payment event without making a real payment:

**Endpoint:** `POST /api/websocket/test-notification`
**Headers:** `Authorization: Bearer <token>`
**Body:**
```json
{
  "PaymentId": "test_123",
  "Status": "authorized",
  "Amount": 100.00
}
```

---

## ⚡ Rate Limits & Performance

We have configured **high-throughput** limits to support heavy traffic:

- **Max Connections:** 10,000 per IP
- **Max Messages:** 10,000 per minute
- **Ban Duration:** 1 minute only

This ensures the system can handle thousands of concurrent users without blocking legitimate traffic.

---

## 📝 API Reference

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/ws` | GET | WebSocket connection endpoint |
| `/api/auth/login` | POST | Get JWT token |
| `/api/websocket/health` | GET | Check system status |
| `/api/websocket/stats` | GET | View active connections count |
| `/api/websocket/connections` | GET | List connected user IDs |

---

## 🔒 Security

- **JWT Authentication:** All connections require a valid signed token.
- **Secure WSS:** Always use `wss://` (SSL) in production.
