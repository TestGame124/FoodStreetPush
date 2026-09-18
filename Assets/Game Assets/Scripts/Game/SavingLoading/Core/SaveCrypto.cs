using System;
using System.Security.Cryptography;

namespace FoodStreet.Saving
{
    /// <summary>
    /// Confidentiality and tamper detection for the save payload.
    ///
    /// AES-256-CBC hides the contents, HMAC-SHA256 proves they were not edited.
    /// The MAC is computed over the ciphertext and header (encrypt-then-MAC), so
    /// an edited file is rejected before a single byte is fed to the decryptor.
    ///
    /// This is client-side protection: anyone with the binary can eventually
    /// recover the key. The goal is to stop the realistic attack - a player
    /// opening the save in a text editor or a generic "game hacker" app and
    /// editing a coin count - not to defeat a determined reverse engineer.
    /// </summary>
    internal static class SaveCrypto
    {
        internal const int IvLength = 16;
        internal const int MacLength = 32;

        private const int KeyLength = 32;
        private const int DerivationRounds = 25000;

        private static readonly object keyGate = new object();
        private static byte[] cipherKey;
        private static byte[] macKey;

        // Fixed salt: the key is derived once per session and cached, and the
        // per-write random IV is what keeps two saves of the same state from
        // producing identical ciphertext.
        private static readonly byte[] KeySalt =
        {
            0x2F, 0xB1, 0x74, 0x0C, 0xA6, 0x53, 0xE9, 0x18,
            0x8D, 0x37, 0x62, 0xCB, 0x40, 0x9A, 0x1E, 0x75
        };

        /// <summary>
        /// The passphrase is never present as one contiguous literal. Three
        /// unrelated-looking arrays are XOR-folded at runtime, so dumping the
        /// strings or constants out of the shipped binary does not hand over
        /// the key.
        /// </summary>
        private static byte[] BuildPassphrase()
        {
            byte[] a =
            {
                0x8E, 0x2D, 0x51, 0xC7, 0x0A, 0xB4, 0x63, 0xF9,
                0x1C, 0x7A, 0xD3, 0x46, 0x95, 0x2B, 0xE0, 0x58,
                0x33, 0xA1, 0x6E, 0xCD, 0x84, 0x1F, 0x72, 0xB6,
                0x09, 0xE5, 0x4C, 0x27, 0xDA, 0x60, 0x9B, 0x3F
            };
            byte[] b =
            {
                0x47, 0xF2, 0x19, 0x8B, 0x5D, 0x30, 0xC6, 0x2E,
                0xA8, 0x04, 0x7F, 0xB1, 0x63, 0xDC, 0x15, 0x92,
                0x6B, 0x38, 0xE7, 0x50, 0xC2, 0x99, 0x0D, 0x41,
                0xF6, 0x2A, 0x87, 0x5E, 0x13, 0xBD, 0x74, 0xC0
            };
            byte[] c =
            {
                0x1B, 0x96, 0x0F, 0x52, 0xE4, 0x7D, 0x38, 0xA0,
                0x65, 0xC9, 0x21, 0x8E, 0x0B, 0x47, 0xF3, 0x2C,
                0x59, 0xD6, 0x14, 0x82, 0x3B, 0xEF, 0x66, 0x08,
                0x9D, 0x71, 0x25, 0xB8, 0xC4, 0x4A, 0x0E, 0x93
            };

            byte[] passphrase = new byte[a.Length];
            for (int i = 0; i < passphrase.Length; i++)
            {
                passphrase[i] = (byte)(a[i] ^ b[i] ^ c[i]);
            }

            Array.Clear(a, 0, a.Length);
            Array.Clear(b, 0, b.Length);
            Array.Clear(c, 0, c.Length);
            return passphrase;
        }

        private static void EnsureKeys()
        {
            if (cipherKey != null)
                return;

            lock (keyGate)
            {
                if (cipherKey != null)
                    return;

                byte[] passphrase = BuildPassphrase();
                byte[] material = Pbkdf2(passphrase, KeySalt, DerivationRounds, KeyLength * 2);
                Array.Clear(passphrase, 0, passphrase.Length);

                byte[] derivedCipherKey = new byte[KeyLength];
                byte[] derivedMacKey = new byte[KeyLength];
                Buffer.BlockCopy(material, 0, derivedCipherKey, 0, KeyLength);
                Buffer.BlockCopy(material, KeyLength, derivedMacKey, 0, KeyLength);
                Array.Clear(material, 0, material.Length);

                macKey = derivedMacKey;
                // Published last: cipherKey doubles as the "keys are ready" flag
                // for the unlocked fast path above.
                cipherKey = derivedCipherKey;
            }
        }

        /// <summary>
        /// PBKDF2-HMAC-SHA256, spelled out rather than taken from
        /// Rfc2898DeriveBytes so the result is identical on every scripting
        /// backend and platform Unity builds for.
        /// </summary>
        private static byte[] Pbkdf2(byte[] password, byte[] salt, int rounds, int outputLength)
        {
            byte[] output = new byte[outputLength];
            using (HMACSHA256 hmac = new HMACSHA256(password))
            {
                int hashLength = hmac.HashSize / 8;
                int blockCount = (outputLength + hashLength - 1) / hashLength;
                byte[] block = new byte[salt.Length + 4];
                Buffer.BlockCopy(salt, 0, block, 0, salt.Length);

                int written = 0;
                for (int blockIndex = 1; blockIndex <= blockCount; blockIndex++)
                {
                    block[salt.Length + 0] = (byte)(blockIndex >> 24);
                    block[salt.Length + 1] = (byte)(blockIndex >> 16);
                    block[salt.Length + 2] = (byte)(blockIndex >> 8);
                    block[salt.Length + 3] = (byte)blockIndex;

                    byte[] u = hmac.ComputeHash(block);
                    byte[] accumulator = (byte[])u.Clone();

                    for (int round = 1; round < rounds; round++)
                    {
                        u = hmac.ComputeHash(u);
                        for (int i = 0; i < accumulator.Length; i++)
                        {
                            accumulator[i] ^= u[i];
                        }
                    }

                    int copyLength = Math.Min(hashLength, outputLength - written);
                    Buffer.BlockCopy(accumulator, 0, output, written, copyLength);
                    written += copyLength;
                }
            }
            return output;
        }

        internal static byte[] NewIv()
        {
            byte[] iv = new byte[IvLength];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }
            return iv;
        }

        internal static byte[] Encrypt(byte[] plain, byte[] iv)
        {
            EnsureKeys();
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeyLength * 8;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = cipherKey;
                aes.IV = iv;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    return encryptor.TransformFinalBlock(plain, 0, plain.Length);
                }
            }
        }

        internal static byte[] Decrypt(byte[] cipher, int offset, int count, byte[] iv)
        {
            EnsureKeys();
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeyLength * 8;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = cipherKey;
                aes.IV = iv;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(cipher, offset, count);
                }
            }
        }

        internal static byte[] ComputeMac(byte[] buffer, int offset, int count)
        {
            EnsureKeys();
            using (HMACSHA256 hmac = new HMACSHA256(macKey))
            {
                return hmac.ComputeHash(buffer, offset, count);
            }
        }

        /// <summary>
        /// Length-independent, early-exit-free comparison, so a caller cannot
        /// learn the expected MAC one byte at a time from timing.
        /// </summary>
        internal static bool MacEquals(byte[] expected, byte[] actual, int actualOffset)
        {
            if (expected == null || actual == null)
                return false;
            if (actual.Length - actualOffset < expected.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                difference |= expected[i] ^ actual[actualOffset + i];
            }
            return difference == 0;
        }
    }
}
