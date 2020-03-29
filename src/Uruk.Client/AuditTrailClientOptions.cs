namespace Uruk.Client
{
    public class AuditTrailClientOptions
    {
        public string EventEndpoint { get; set; }

        public byte[] EncryptionKey { get; set; }

        public int ReloadTokenIntervalInSeconds { get; set; } = 60;
    }
}