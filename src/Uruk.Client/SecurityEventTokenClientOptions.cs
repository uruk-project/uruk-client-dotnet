namespace Uruk.Client
{
    public class SecurityEventTokenClientOptions
    {
        public string EventEndpoint { get; set; } = "/events";
  
        public byte[] EncryptionKey { get; set; }
    }
}