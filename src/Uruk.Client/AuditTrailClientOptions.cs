using JsonWebToken;

namespace Uruk.Client
{
    public class AuditTrailClientOptions
    {
        /// <summary>
        /// Gets or sets URL to the audit trail hub endpoint.
        /// </summary>
        public string EventEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the symmetric key used for encrypting the audit trail stored in case of recoverable error. If <c>null</c>, the audit trail will be stored in plaintext.
        /// </summary>
        public SymmetricJwk? StorageEncryptionKey { get; set; }

        /// <summary>
        ///Gets or sets the inverval in seconds between each attempt to resend a stored audit trail.
        /// </summary>
        public int ResendIntervalInSeconds { get; set; } = 60;
      
        /// <summary>
        /// Gets or sets the path for storing temporary audit trails.  
        /// </summary>
        public string? StoragePath { get; set; }
    }
}