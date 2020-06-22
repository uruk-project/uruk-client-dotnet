using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Uruk.Client
{
    /// <summary>
    /// This store record the token in the file system
    /// </summary>
    public class DefaultAuditTrailStore : IAuditTrailStore
    {
        private static readonly Token EmptyToken = new Token();

        private readonly SymmetricJwk? _encryptionKey;
        private readonly JwtWriter? _writer;
        private readonly JwtReader? _reader;
        private readonly TokenValidationPolicy? _policy;
        private readonly AuditTrailClientOptions _options;
        private readonly ILogger<DefaultAuditTrailStore> _logger;
        private readonly string _directory;

        public DefaultAuditTrailStore(IOptions<AuditTrailClientOptions> options, ILogger<DefaultAuditTrailStore> logger)
        {
            _options = options.Value;
            _logger = logger;
            if (_options.TemporaryStoragePath is null)
            {
                const string auditTrailFallbackDir = "AUDITTRAIL_FALLBACK_DIR";
                var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                            ?? Environment.GetEnvironmentVariable(auditTrailFallbackDir);

                if (string.IsNullOrEmpty(root))
                {
                    throw new InvalidOperationException("Could not determine an appropriate location for storing tokens. Set the " + auditTrailFallbackDir + " environment variable to a folder where tokens should be stored.");
                }

                _directory = Path.Combine(root, Constants.DefaultStorageDirectory);
            }
            else
            {
                _directory = _options.TemporaryStoragePath;
            }

            try
            {
                if (ContainerUtils.IsContainer && !ContainerUtils.IsVolumeMountedFolder(_directory))
                {
                    // warn users that tokens may be lost when running in docker without a volume mounted folder
                    _logger.UsingEphemeralFileSystemLocationInContainer(_directory);
                }
            }
            catch (Exception ex)
            {
                // Treat exceptions as non-fatal when attempting to detect docker.
                // These might occur if fstab is an unrecognized format, or if there are other unusual
                // file IO errors.
                _logger.LogTrace(ex, "Failure occurred while attempting to detect docker.");
            }

            if (_options.TemporaryStorageEncryptionKey != null)
            {
                _encryptionKey = _options.TemporaryStorageEncryptionKey;
                _writer = new JwtWriter();
                _reader = new JwtReader(_encryptionKey);
                _policy = new TokenValidationPolicyBuilder()
                    .IgnoreNestedToken()
                    .IgnoreSignature()
                    .Build();
            }
            else
            {
                _logger.LogWarning("No encryption key is defined. The audit trail will be stored in plaintext.");
            }
        }

        public IEnumerable<Token> GetAllAuditTrailRecords()
        {
            if (Directory.Exists(_directory))
            {
                foreach (var filename in Directory.EnumerateFiles(_directory, "*.token", SearchOption.TopDirectoryOnly))
                {
                    var token = ReadTokenFromFile(filename);
                    if (token.Value != null)
                    {
                        yield return token;
                    }
                }
            }
        }

        private Token ReadTokenFromFile(string filename)
        {
            try
            {
                var data = File.ReadAllBytes(filename);
                if (_reader is null)
                {
                    return new Token(data, filename, 0);
                }
                else
                {
                    var result = _reader.TryReadToken(data, _policy!);
                    if (result.Succedeed)
                    {
                        return new Token(result.Token!.Binary!, filename, 0);
                    }
                    else
                    {
                        _logger.ReadingTokenFileFailed(filename);
                        return EmptyToken;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.ReadingTokenFileFailed(filename, e);
                return EmptyToken;
            }
        }

        public async Task<string> RecordAuditTrailAsync(byte[] token)
        {
            int length = token.Length;
            var bufferWriter = new PooledByteBufferWriter(length + 512);
            if (_writer is null)
            {
                var buffer = bufferWriter.GetMemory(length);
                token.AsMemory().CopyTo(buffer);
                bufferWriter.Advance(length);
            }
            else
            {
                JwtDescriptor descriptor = new BinaryJweDescriptor(token)
                {
                    Algorithm = KeyManagementAlgorithm.Direct,
                    EncryptionAlgorithm = EncryptionAlgorithm.Aes128CbcHmacSha256,
                    CompressionAlgorithm = CompressionAlgorithm.Deflate,
                    EncryptionKey = _encryptionKey!
                };

                _writer.WriteToken(descriptor, bufferWriter);
            }

            Directory.CreateDirectory(_directory);
            var finalFilename = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".token");
            var tempFilename = finalFilename + ".tmp";

            try
            {
                using (var tempFileStream = File.OpenWrite(tempFilename))
                {
#if NETSTANDARD2_0
                    await tempFileStream.WriteAsync(bufferWriter.Buffer, 0, bufferWriter.WrittenCount);
#else
                    await tempFileStream.WriteAsync(bufferWriter.WrittenMemory);
#endif
                }

                _logger.WritingTokenToFile(finalFilename);

                try
                {
                    // Once the file has been fully written, perform the rename.
                    File.Move(tempFilename, finalFilename);
                }
                catch (IOException)
                {
                    File.Copy(tempFilename, finalFilename);
                }
            }
            finally
            {
                File.Delete(tempFilename);
            }

            return finalFilename;
        }

        public void DeleteRecord(Token token)
        {
            try
            {
                File.Delete(token.Filename);
            }
            catch   (Exception exception)
            {
                _logger.LogWarning(exception, "File {filename} not found when trying to delete", token.Filename);
            }
        }
    }
}
