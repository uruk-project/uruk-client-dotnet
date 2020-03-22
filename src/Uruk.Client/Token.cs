namespace Uruk.Client
{
    public struct Token
    {
        public Token(byte[] value, int retryCount)
        {
            Value = value;
            RetryCount = retryCount;
        }

        public byte[] Value { get; set; }

        public int RetryCount { get; set; }
    }
}