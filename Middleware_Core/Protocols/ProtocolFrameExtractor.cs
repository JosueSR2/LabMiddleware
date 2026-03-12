using System.Text;
using Middleware_Core.Configuration;

namespace Middleware_Core.Protocols
{
    public static class ProtocolFrameExtractor
    {
        private const char Vt = (char)0x0B;
        private const char Fs = (char)0x1C;
        private const char Cr = (char)0x0D;
        private const char Lf = (char)0x0A;
        private const char Stx = (char)0x02;
        private const char Etx = (char)0x03;
        private const char Eot = (char)0x04;

        public static IEnumerable<string> ExtractMessages(StringBuilder buffer, ProtocolType protocol)
        {
            return protocol switch
            {
                ProtocolType.Hl7Mllp => ExtractMllpMessages(buffer),
                ProtocolType.Astm => ExtractAstmMessages(buffer),
                _ => ExtractRawMessages(buffer)
            };
        }

        public static byte[] BuildAck(ProtocolType protocol)
        {
            if (protocol == ProtocolType.Hl7Mllp)
                return Encoding.UTF8.GetBytes($"{Vt}MSA|AA{Fs}{Cr}");

            if (protocol == ProtocolType.Astm)
                return new byte[] { 0x06 };

            return Array.Empty<byte>();
        }

        public static byte[] BuildNak(ProtocolType protocol)
        {
            if (protocol == ProtocolType.Astm)
                return new byte[] { 0x15 };

            return Array.Empty<byte>();
        }

        private static IEnumerable<string> ExtractRawMessages(StringBuilder buffer)
        {
            if (buffer.Length == 0)
                yield break;

            var raw = buffer.ToString();
            buffer.Clear();
            yield return raw;
        }

        private static IEnumerable<string> ExtractMllpMessages(StringBuilder buffer)
        {
            while (true)
            {
                var payload = buffer.ToString();
                var start = payload.IndexOf(Vt);
                if (start < 0)
                    yield break;

                var end = payload.IndexOf($"{Fs}{Cr}", start, StringComparison.Ordinal);
                if (end < 0)
                    yield break;

                var messageStart = start + 1;
                var message = payload.Substring(messageStart, end - messageStart);

                buffer.Remove(0, end + 2);
                yield return message;
            }
        }

        private static IEnumerable<string> ExtractAstmMessages(StringBuilder buffer)
        {
            var records = new List<string>();

            while (buffer.Length > 0)
            {
                var value = buffer[0];

                if (value == Eot)
                {
                    buffer.Remove(0, 1);
                    if (records.Count > 0)
                    {
                        yield return string.Join("\n", records);
                        records.Clear();
                    }
                    continue;
                }

                if (value == Stx)
                {
                    var etxPos = buffer.ToString().IndexOf(Etx, 1);
                    if (etxPos < 0 || etxPos + 2 >= buffer.Length)
                        yield break;

                    var checksumHex = $"{buffer[etxPos + 1]}{buffer[etxPos + 2]}";
                    var frameBody = buffer.ToString(1, etxPos - 1);
                    var computed = ComputeChecksumHex($"{frameBody}{Etx}");

                    buffer.Remove(0, etxPos + 3);
                    if (buffer.Length > 0 && buffer[0] == Cr)
                        buffer.Remove(0, 1);
                    if (buffer.Length > 0 && buffer[0] == Lf)
                        buffer.Remove(0, 1);

                    if (string.Equals(computed, checksumHex, StringComparison.OrdinalIgnoreCase))
                        records.Add(StripAstmFrameNumber(frameBody));

                    continue;
                }

                buffer.Remove(0, 1);
            }

            if (records.Count > 0)
                yield return string.Join("\n", records);
        }

        private static string StripAstmFrameNumber(string frameBody)
        {
            if (string.IsNullOrEmpty(frameBody))
                return frameBody;

            // ASTM LIS2-A2 frames begin with a single ASCII frame number (0-7) right
            // after STX. OpenELIS readers expect lines to start with "H|", "P|", etc.
            var first = frameBody[0];
            return first is >= '0' and <= '7' && frameBody.Length > 1
                ? frameBody[1..]
                : frameBody;
        }

        private static string ComputeChecksumHex(string input)
        {
            var bytes = Encoding.ASCII.GetBytes(input);
            int sum = 0;
            foreach (var b in bytes)
                sum += b;

            return (sum & 0xFF).ToString("X2");
        }
    }
}
