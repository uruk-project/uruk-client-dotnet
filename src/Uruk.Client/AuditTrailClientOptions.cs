using System;
using System.Net.Http;
using System.Security.Principal;
using System.Threading.Tasks;
using IdentityModel.Client;
using JsonWebToken;

namespace Uruk.Client
{
    public class AuditTrailClientOptions
    {
        /// <summary>
        /// Gets or sets the URL to the delivery endpoint.
        /// </summary>
        public string? DeliveryEndpoint { get; set; }

        /// <summary>
        ///Gets or sets the inverval in seconds between each attempt to resend a stored audit trail.
        /// </summary>
        public int ResendIntervalInSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the path for storing temporary audit trails.  
        /// </summary>
        public string? TemporaryStoragePath { get; set; }

        /// <summary>
        /// Gets or sets the symmetric key used for encrypting the audit trail stored in case of recoverable error. If <c>null</c>, the audit trail will be stored in plaintext.
        /// </summary>
        public SymmetricJwk? TemporaryStorageEncryptionKey { get; set; }

        /// <summary>
        /// Gets or sets the scope required for the access_token.
        /// </summary>
        public string? AccessTokenScope { get; set; }

        /// <summary>
        /// Gets or sets the authorization scheme. The default value is "Bearer".
        /// </summary>
        public string AuthorizationScheme { get; set; } = "Bearer";

        /// <summary>
        /// Gets or sets the options for acquiring an access token.
        /// </summary>
        public TokenClientOptions TokenClientOptions { get; internal set; } = new TokenClientOptions();
}
}