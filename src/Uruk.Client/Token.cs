namespace Uruk.Client
{
    public struct Token
    {
        public byte[] Value { get; set; }

        public int RetryCount { get; set; }
    }
}