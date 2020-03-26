using System;

namespace Uruk.Client
{
    public struct Token
    {
        public Token(byte[] value, string filename, int retryCount)
        {
            Value = value;
            Filename = filename;
            RetryCount = retryCount;
        }

        public byte[] Value { get; set; }

        public int RetryCount { get; set; }

        public string Filename { get; set; }
    }
}