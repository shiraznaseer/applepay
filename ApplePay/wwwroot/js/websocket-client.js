// Enhanced WebSocket Client for Payment Updates
class PaymentWebSocketClient {
    constructor(options = {}) {
        this.url = options.url || this.getDefaultWebSocketUrl();
        this.token = options.token || '';
        this.userId = options.userId || null;
        this.autoReconnect = options.autoReconnect !== false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = options.maxReconnectAttempts || 10;
        this.reconnectDelay = options.reconnectDelay || 1000;
        this.heartbeatInterval = options.heartbeatInterval || 30000;
        this.listeners = {};
        this.ws = null;
        this.heartbeatTimer = null;
        this.connectionState = 'disconnected';
        this.messageQueue = [];
        this.isProcessingQueue = false;
    }

    getDefaultWebSocketUrl() {
        const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
        return `${protocol}//${window.location.host}/ws`;
    }

    connect() {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            console.log('WebSocket is already connected');
            return;
        }

        try {
            // Build WebSocket URL with token
            const wsUrl = new URL(this.url);
            if (this.token) {
                wsUrl.searchParams.append('token', this.token);
            }

            this.connectionState = 'connecting';
            this.ws = new WebSocket(wsUrl.toString());

            this.ws.onopen = () => {
                console.log('WebSocket connected successfully');
                this.connectionState = 'connected';
                this.reconnectAttempts = 0;
                this.startHeartbeat();
                this.emit('connected');
                this.processMessageQueue();
            };

            this.ws.onmessage = (event) => {
                try {
                    const data = JSON.parse(event.data);
                    this.handleMessage(data);
                } catch (error) {
                    console.error('Error parsing WebSocket message:', error);
                    this.emit('error', { type: 'parse_error', message: error.message });
                }
            };

            this.ws.onclose = (event) => {
                console.log('WebSocket disconnected:', event.code, event.reason);
                this.connectionState = 'disconnected';
                this.stopHeartbeat();
                this.emit('disconnected', { code: event.code, reason: event.reason });
                this.handleReconnect();
            };

            this.ws.onerror = (error) => {
                console.error('WebSocket error:', error);
                this.connectionState = 'error';
                this.emit('error', { type: 'websocket_error', error });
            };

        } catch (error) {
            console.error('Failed to connect to WebSocket:', error);
            this.connectionState = 'error';
            this.emit('error', { type: 'connection_error', error });
        }
    }

    handleMessage(data) {
        console.log('WebSocket message received:', data);

        switch (data.type) {
            case 'welcome':
                this.emit('welcome', data);
                break;
            case 'pong':
                this.emit('pong', data);
                break;
            case 'payment.updated':
                this.emit('paymentUpdate', data);
                break;
            case 'subscription_confirmed':
                this.emit('subscriptionConfirmed', data);
                break;
            case 'unsubscription_confirmed':
                this.emit('unsubscriptionConfirmed', data);
                break;
            case 'error':
                this.emit('serverError', data);
                break;
            default:
                this.emit('message', data);
        }
    }

    send(message) {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            try {
                const messageStr = typeof message === 'string' ? message : JSON.stringify(message);
                this.ws.send(messageStr);
                return true;
            } catch (error) {
                console.error('Failed to send WebSocket message:', error);
                this.emit('error', { type: 'send_error', error });
                return false;
            }
        } else {
            console.log('WebSocket not connected, queuing message');
            this.messageQueue.push(message);
            return false;
        }
    }

    sendPing() {
        this.send({ type: 'ping', timestamp: Date.now() });
    }

    subscribe(events = []) {
        this.send({ type: 'subscribe', events });
    }

    unsubscribe(events = []) {
        this.send({ type: 'unsubscribe', events });
    }

    disconnect() {
        this.autoReconnect = false;
        this.stopHeartbeat();
        if (this.ws) {
            this.ws.close(1000, 'Client disconnect');
            this.ws = null;
        }
    }

    handleReconnect() {
        if (this.autoReconnect && this.reconnectAttempts < this.maxReconnectAttempts) {
            this.reconnectAttempts++;
            const delay = this.reconnectDelay * Math.pow(2, this.reconnectAttempts - 1); // Exponential backoff
            
            console.log(`Attempting to reconnect (${this.reconnectAttempts}/${this.maxReconnectAttempts}) in ${delay}ms...`);
            
            setTimeout(() => {
                if (this.autoReconnect) {
                    this.connect();
                }
            }, delay);
        } else if (this.reconnectAttempts >= this.maxReconnectAttempts) {
            console.error('Max reconnection attempts reached');
            this.emit('maxReconnectAttemptsReached');
        }
    }

    startHeartbeat() {
        this.stopHeartbeat();
        this.heartbeatTimer = setInterval(() => {
            if (this.ws && this.ws.readyState === WebSocket.OPEN) {
                this.sendPing();
            }
        }, this.heartbeatInterval);
    }

    stopHeartbeat() {
        if (this.heartbeatTimer) {
            clearInterval(this.heartbeatTimer);
            this.heartbeatTimer = null;
        }
    }

    processMessageQueue() {
        if (this.isProcessingQueue || this.messageQueue.length === 0) {
            return;
        }
        this.isProcessingQueue = true;

        while (this.messageQueue.length > 0) {
            const message = this.messageQueue.shift();
            this.send(message);
        }

        this.isProcessingQueue = false;
    }

    on(event, callback) {
        if (!this.listeners[event]) {
            this.listeners[event] = [];
        }
        this.listeners[event].push(callback);
    }

    off(event, callback) {
        if (this.listeners[event]) {
            this.listeners[event] = this.listeners[event].filter(cb => cb !== callback);
        }
    }

    emit(event, data) {
        if (this.listeners[event]) {
            this.listeners[event].forEach(callback => {
                try {
                    callback(data);
                } catch (error) {
                    console.error(`Error in event listener for ${event}:`, error);
                }
            });
        }
    }

    getState() {
        return {
            connectionState: this.connectionState,
            reconnectAttempts: this.reconnectAttempts,
            queuedMessages: this.messageQueue.length,
            url: this.url,
            userId: this.userId
        };
    }

    // Static method to create client with JWT from localStorage or cookie
    static createWithAutoToken(options = {}) {
        let token = options.token;
        
        // Try to get token from localStorage
        if (!token) {
            token = localStorage.getItem('jwt_token');
        }
        
        // Try to get token from cookie
        if (!token) {
            const cookies = document.cookie.split(';');
            for (const cookie of cookies) {
                const [name, value] = cookie.trim().split('=');
                if (name === 'access_token' || name === 'jwt_token') {
                    token = value;
                    break;
                }
            }
        }

        if (!token) {
            console.warn('No JWT token found. WebSocket connection may fail authentication.');
        }

        return new PaymentWebSocketClient({
            ...options,
            token: token || ''
        });
    }
}

// Usage examples and utilities
window.PaymentWebSocketClient = PaymentWebSocketClient;

// Example usage:
/*
// Basic usage
const client = new PaymentWebSocketClient({
    token: 'your-jwt-token-here',
    autoReconnect: true,
    maxReconnectAttempts: 5
});

client.on('connected', () => {
    console.log('Connected to payment updates!');
    client.subscribe(['payment.updated']);
});

client.on('paymentUpdate', (data) => {
    console.log('Payment updated:', data);
    // Handle payment update:
    // {
    //   "event": "payment.updated",
    //   "paymentId": "pay_abc123",
    //   "orderReferenceId": "order_987", 
    //   "status": "authorized",
    //   "amount": 123.45,
    //   "timestamp": "2025-12-03T19:13:00.000Z"
    // }
    
    // Update UI, show notification, etc.
    showPaymentNotification(data);
});

client.on('disconnected', () => {
    console.log('Disconnected from WebSocket');
});

client.on('error', (error) => {
    console.error('WebSocket error:', error);
});

client.connect();

// Auto-create client with token from localStorage/cookie
const autoClient = PaymentWebSocketClient.createWithAutoToken({
    autoReconnect: true
});

// Utility function to show payment notifications
function showPaymentNotification(paymentData) {
    const notification = new Notification(`Payment ${paymentData.status}`, {
        body: `Payment ${paymentData.paymentId} for order ${paymentData.orderReferenceId} is now ${paymentData.status}`,
        icon: '/icon.png'
    });
    
    notification.onclick = () => {
        window.focus();
        notification.close();
    };
}

// Request notification permission
if ('Notification' in window && Notification.permission === 'default') {
    Notification.requestPermission();
}
*/
