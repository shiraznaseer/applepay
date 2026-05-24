namespace ApplePay.Models.HyperPay
{
    public static class HyperPayResultCodes
    {
        // Success codes
        public const string SuccessfullyProcessed = "000.000.000";
        public const string SuccessfullyCreatedCheckout = "000.200.100";
        public const string SuccessfullyAuthorized = "000.100.110";
        public const string SuccessfullyCaptured = "000.100.900";
        public const string SuccessfullyRefunded = "000.100.200";
        public const string SuccessfullyReversed = "000.200.400";
        
        // Pending codes
        public const string PaymentPending = "000.200.000";
        public const string AuthenticationPending = "000.200.010";
        public const string RiskManagementPending = "000.200.020";
        public const string RiskManagementCheckPending = "000.200.030";
        
        // Error codes
        public const string InvalidRequestFormat = "800.400.001";
        public const string InvalidEntityId = "800.400.100";
        public const string InvalidAccessToken = "800.400.102";
        public const string InvalidPaymentBrand = "800.400.200";
        public const string InvalidApplePayToken = "900.400.300";
        public const string InvalidCardData = "800.400.210";
        public const string InvalidAmount = "800.400.150";
        public const string InvalidCurrency = "800.400.151";
        public const string Invalid3DSecureData = "800.400.300";
        
        // Communication errors
        public const string CommunicationError = "800.100.100";
        public const string SystemError = "800.100.150";
        public const string TimeoutError = "800.100.200";
        
        // Risk management errors
        public const string RiskManagementDecline = "800.100.300";
        public const string FraudDetectionDecline = "800.100.301";
        public const string BlacklistedCard = "800.100.302";
        public const string InvalidCVV = "800.100.400";
        public const string InvalidExpiry = "800.100.401";
        
        // 3D Secure errors
        public const string ThreeDSecureNotSupported = "800.100.500";
        public const string ThreeDSecureAuthenticationFailed = "800.100.501";
        public const string ThreeDSecureTimeout = "800.100.502";
        
        public static bool IsSuccess(string resultCode)
        {
            return resultCode.StartsWith("000.") && 
                   (resultCode == SuccessfullyProcessed || 
                    resultCode == SuccessfullyCreatedCheckout || 
                    resultCode == SuccessfullyAuthorized || 
                    resultCode == SuccessfullyCaptured || 
                    resultCode == SuccessfullyRefunded || 
                    resultCode == SuccessfullyReversed);
        }
        
        public static bool IsPending(string resultCode)
        {
            return resultCode.StartsWith("000.") && 
                   (resultCode == PaymentPending || 
                    resultCode == AuthenticationPending || 
                    resultCode == RiskManagementPending || 
                    resultCode == RiskManagementCheckPending);
        }
        
        public static bool IsError(string resultCode)
        {
            return resultCode.StartsWith("800.") || resultCode.StartsWith("900.");
        }
        
        public static string GetResultDescription(string resultCode)
        {
            return resultCode switch
            {
                SuccessfullyProcessed => "Successfully processed",
                SuccessfullyCreatedCheckout => "Successfully created checkout",
                SuccessfullyAuthorized => "Successfully authorized",
                SuccessfullyCaptured => "Successfully captured",
                SuccessfullyRefunded => "Successfully refunded",
                SuccessfullyReversed => "Successfully reversed",
                PaymentPending => "Payment is pending",
                AuthenticationPending => "Authentication is pending",
                RiskManagementPending => "Risk management is pending",
                RiskManagementCheckPending => "Risk management check is pending",
                InvalidRequestFormat => "Invalid request format",
                InvalidEntityId => "Invalid entity ID",
                InvalidAccessToken => "Invalid access token",
                InvalidPaymentBrand => "Invalid payment brand",
                InvalidApplePayToken => "Invalid Apple Pay token",
                InvalidCardData => "Invalid card data",
                InvalidAmount => "Invalid amount",
                InvalidCurrency => "Invalid currency",
                Invalid3DSecureData => "Invalid 3D Secure data",
                CommunicationError => "Communication error",
                SystemError => "System error",
                TimeoutError => "Timeout error",
                RiskManagementDecline => "Risk management decline",
                FraudDetectionDecline => "Fraud detection decline",
                BlacklistedCard => "Blacklisted card",
                InvalidCVV => "Invalid CVV",
                InvalidExpiry => "Invalid expiry",
                ThreeDSecureNotSupported => "3D Secure not supported",
                ThreeDSecureAuthenticationFailed => "3D Secure authentication failed",
                ThreeDSecureTimeout => "3D Secure timeout",
                _ => "Unknown result code"
            };
        }
    }
}
