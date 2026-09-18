using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace FoodStreet.Saving
{
    public enum SaveReadStatus
    {
        /// <summary>File was read, verified and decoded.</summary>
        Ok,

        /// <summary>Nothing on disk yet - a brand new player.</summary>
        Missing,

        /// <summary>Not one of our files, or truncated below the header size.</summary>
        BadFormat,

        /// <summary>Header parsed but the MAC did not match: hand-edited or bit-rotted.</summary>
        Tampered,

        /// <summary>The file exists but the OS or the decoder refused it.</summary>
        Unreadable
    }

    /// <summary>
    /// The on-disk container: a fixed header, the compressed+encrypted payload,
    /// and a trailing HMAC over everything before it.
    ///
    /// <code>
    ///   0  int    magic  'F','S','V','1'
    ///   4  int    envelope version
    ///   8  int    flags (1 = encrypted, 2 = gzip)
    ///  12  int    reserved
    ///  16  byte[16] iv
    ///  32  int    payload length
    ///  36  byte[] payload
    ///   -  byte[32] HMAC-SHA256 over bytes [0, 36 + payload length)
    /// </code>
    /// </summary>
    internal static class SaveEnvelope
    {
        private const int Magic = 0x31565346; // "FSV1" read little-endian
        private const int CurrentVersion = 1;

        private const int FlagEncrypted = 1 << 0;
        private const int FlagCompressed = 1 << 1;

        private const int OffsetMagic = 0;
        private const int OffsetVersion = 4;
        private const int OffsetFlags = 8;
        private const int OffsetReserved = 12;
        private const int OffsetIv = 16;
        private const int OffsetPayloadLength = OffsetIv + SaveCrypto.IvLength;
        private const int HeaderLength = OffsetPayloadLength + 4;

        internal static byte[] Pack(string json)
        {
            byte[] raw = Encoding.UTF8.GetBytes(json);
            byte[] compressed = Compress(raw);
            byte[] iv = SaveCrypto.NewIv();
            byte[] payload = SaveCrypto.Encrypt(compressed, iv);

            byte[] file = new byte[HeaderLength + payload.Length + SaveCrypto.MacLength];

            WriteInt(file, OffsetMagic, Magic);
            WriteInt(file, OffsetVersion, CurrentVersion);
            WriteInt(file, OffsetFlags, FlagEncrypted | FlagCompressed);
            WriteInt(file, OffsetReserved, 0);
            Buffer.BlockCopy(iv, 0, file, OffsetIv, SaveCrypto.IvLength);
            WriteInt(file, OffsetPayloadLength, payload.Length);
            Buffer.BlockCopy(payload, 0, file, HeaderLength, payload.Length);

            int macCoverage = HeaderLength + payload.Length;
            byte[] mac = SaveCrypto.ComputeMac(file, 0, macCoverage);
            Buffer.BlockCopy(mac, 0, file, macCoverage, SaveCrypto.MacLength);

            return file;
        }

        internal static SaveReadStatus TryUnpack(byte[] file, out string json)
        {
            json = null;

            if (file == null || file.Length < HeaderLength + SaveCrypto.MacLength)
                return SaveReadStatus.BadFormat;
            if (ReadInt(file, OffsetMagic) != Magic)
                return SaveReadStatus.BadFormat;

            int version = ReadInt(file, OffsetVersion);
            if (version <= 0 || version > CurrentVersion)
                return SaveReadStatus.BadFormat;

            int payloadLength = ReadInt(file, OffsetPayloadLength);
            if (payloadLength < 0)
                return SaveReadStatus.BadFormat;

            int macCoverage = HeaderLength + payloadLength;
            // Guard against an overflowed or oversized length before it is used
            // as an offset into the buffer.
            if (macCoverage < HeaderLength || file.Length != macCoverage + SaveCrypto.MacLength)
                return SaveReadStatus.BadFormat;

            byte[] expectedMac;
            try
            {
                expectedMac = SaveCrypto.ComputeMac(file, 0, macCoverage);
            }
            catch (Exception)
            {
                return SaveReadStatus.Unreadable;
            }

            if (!SaveCrypto.MacEquals(expectedMac, file, macCoverage))
                return SaveReadStatus.Tampered;

            int flags = ReadInt(file, OffsetFlags);

            try
            {
                byte[] payload;
                if ((flags & FlagEncrypted) != 0)
                {
                    byte[] iv = new byte[SaveCrypto.IvLength];
                    Buffer.BlockCopy(file, OffsetIv, iv, 0, SaveCrypto.IvLength);
                    payload = SaveCrypto.Decrypt(file, HeaderLength, payloadLength, iv);
                }
                else
                {
                    payload = new byte[payloadLength];
                    Buffer.BlockCopy(file, HeaderLength, payload, 0, payloadLength);
                }

                if ((flags & FlagCompressed) != 0)
                {
                    payload = Decompress(payload);
                }

                json = Encoding.UTF8.GetString(payload);
                return SaveReadStatus.Ok;
            }
            catch (Exception)
            {
                // The MAC already passed, so this is a decoder mismatch rather
                // than an edit - treat it as unreadable, not as tampering.
                return SaveReadStatus.Unreadable;
            }
        }

        private static byte[] Compress(byte[] raw)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress, true))
                {
                    gzip.Write(raw, 0, raw.Length);
                }
                return output.ToArray();
            }
        }

        private static byte[] Decompress(byte[] compressed)
        {
            using (MemoryStream input = new MemoryStream(compressed))
            using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int read;
                while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                }
                return output.ToArray();
            }
        }

        private static void WriteInt(byte[] buffer, int offset, int value)
        {
            buffer[offset + 0] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static int ReadInt(byte[] buffer, int offset)
        {
            return buffer[offset]
                 | (buffer[offset + 1] << 8)
                 | (buffer[offset + 2] << 16)
                 | (buffer[offset + 3] << 24);
        }
    }
}
