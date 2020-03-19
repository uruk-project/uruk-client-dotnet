using System.Net.Http;
using System.Net.Http.Headers;
using JsonWebToken;

namespace Uruk.Client
{
    public class SecurityEventTokenPushRequest : HttpRequestMessage
    {
        private static readonly JwtWriter _writer = new JwtWriter(); 
        
        public SecurityEventTokenPushRequest(SecurityEventTokenPushRequest other)
        {
            RequestUri = other.RequestUri;
            Method = other.Method;
            Content = other.Content;
            Content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");
            Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            Token = other.Token;
        }

        public SecurityEventTokenPushRequest(string requestUri, SecurityEventTokenDescriptor token)
            : base(HttpMethod.Post, requestUri)
        {
            using var bufferWriter = new PooledByteBufferWriter(1024);
            _writer.WriteToken(token, bufferWriter);
#if NETSTANDARD2_0
            Content = new ByteArrayContent(bufferWriter.Buffer, 0, bufferWriter.Index);
#else
            Content = new ReadOnlyMemoryContent(bufferWriter.WrittenMemory);
#endif
            Content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");
            Token = token;
            Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public SecurityEventTokenDescriptor Token { get; set; }

        /// <summary>
        /// The delay in milliseconds before to retry to send again the token.
        /// </summary>
        public int RetryDelay { get; internal set; } = 1000;
    }
}