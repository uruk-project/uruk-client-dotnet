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
        private static readonly AuditTrailItem EmptyToken = new AuditTrailItem();

        private readonly SymmetricJwk? _encryptionKey;
        private readonly JwtWriter? _writer;
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
                _policy = new TokenValidationPolicyBuilder()
                    .IgnoreNestedToken()
                    .IgnoreSignatureByDefault()
                    .WithDecryptionKey(_encryptionKey)
                    .Build();
            }
            else
            {
                _logger.LogWarning("No encryption key is defined. The audit trail will be stored in plaintext.");
            }
        }

        public IEnumerable<AuditTrailItem> GetAllAuditTrailRecords()
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

        private AuditTrailItem ReadTokenFromFile(string filename)
        {
            try
            {
                var data = File.ReadAllBytes(filename);
                if (_options.TemporaryStorageEncryptionKey is null)
                {
                    return new AuditTrailItem(data, filename, 0);
                }
                else
                {
                    if (Jwt.TryParse(data, _policy!, out var jwt))
                    {
                        return new AuditTrailItem(jwt.RawValue.ToArray(), filename, 0);
                    }
                    else
                    {
                        _logger.ReadingTokenFileFailed(filename, jwt.Error!.Status, jwt.Error.Exception);
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
            using var bufferWriter = new PooledByteBufferWriter(length + 512);
            if (_writer is null)
            {
                var buffer = bufferWriter.GetMemory(length);
                token.AsMemory().CopyTo(buffer);
                bufferWriter.Advance(length);
            }
            else
            {
                JwtDescriptor descriptor = new BinaryJweDescriptor(
                    _encryptionKey!, 
                    KeyManagementAlgorithm.Dir, 
                    EncryptionAlgorithm.A128CbcHS256, 
                    CompressionAlgorithm.Def)
                {
                   Payload = token
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

        public void DeleteRecord(AuditTrailItem token)
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
