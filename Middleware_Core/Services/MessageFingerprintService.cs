using System.Security.Cryptography;
using System.Text;
using Middleware_Core.Models;

namespace Middleware_Core.Services
{
    public static class MessageFingerprintService
    {
        public static string Build(LabResult result)
        {
            var raw = string.Join("|", new[]
            {
                result.SourceMachine ?? string.Empty,
                result.SampleId ?? string.Empty,
                result.TestCode ?? string.Empty,
                result.Value ?? string.Empty,
                result.Units ?? string.Empty,
                result.Flag ?? string.Empty
            });

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }

        public static string BuildRawMessage(string sourceMachine, string? externalId, string rawMessage)
        {
            var raw = string.Join("|", new[]
            {
                sourceMachine ?? string.Empty,
                externalId ?? string.Empty,
                rawMessage ?? string.Empty
            });

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }
    }
}
