using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace gishadev.tools.SavingSystem
{
    /// <summary>AES-256-CBC with a fresh IV per save and an HMAC-SHA256 tag over the result.
    /// <para><b>This is obfuscation, not security.</b> The passphrase ships inside the build and can be
    /// recovered from it, so a determined player can always read or rewrite their own save. What this
    /// does buy you: saves aren't editable in a text editor, and corruption or casual tampering is
    /// detected instead of being loaded as garbage.</para>
    /// <para>Layout: <c>[IV 16][ciphertext][tag 32]</c>.</para></summary>
    public sealed class AesEncryptor : ISaveEncryptor
    {
        const int IV_SIZE = 16;
        const int TAG_SIZE = 32;

        // Not a secret — see the class summary. Pass your own to the constructor if you'd rather not
        // share a passphrase with every other project using this package.
        const string DEFAULT_PASSPHRASE = "zK3rN5m#DhX8[CYE%'Q={?M(#`-eqYA7";

        readonly byte[] _encryptionKey;
        readonly byte[] _macKey;

        public AesEncryptor(string passphrase = DEFAULT_PASSPHRASE)
        {
            if (string.IsNullOrEmpty(passphrase))
                throw new ArgumentException("Passphrase must not be empty.", nameof(passphrase));

            // Hashing the UTF-8 bytes gives exactly the 32 bytes AES-256 wants from a passphrase of any
            // length or alphabet. The old code did Encoding.ASCII.GetBytes, which silently turned every
            // non-ASCII character into '?' and demanded the caller count to 32 by hand.
            _encryptionKey = Sha256(passphrase);
            _macKey = Sha256(passphrase + "|mac"); // separate key so the tag can't be forged from the cipher key
        }

        public byte[] Encrypt(string plainText)
        {
            if (plainText == null)
                throw new ArgumentNullException(nameof(plainText));

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _encryptionKey;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.GenerateIV();

                    byte[] cipherBody;
                    using (var encryptor = aes.CreateEncryptor())
                    using (var memory = new MemoryStream())
                    {
                        using (var crypto = new CryptoStream(memory, encryptor, CryptoStreamMode.Write))
                        {
                            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                            crypto.Write(plainBytes, 0, plainBytes.Length);
                            crypto.FlushFinalBlock();
                        }

                        cipherBody = memory.ToArray();
                    }

                    byte[] result = new byte[IV_SIZE + cipherBody.Length + TAG_SIZE];
                    Buffer.BlockCopy(aes.IV, 0, result, 0, IV_SIZE);
                    Buffer.BlockCopy(cipherBody, 0, result, IV_SIZE, cipherBody.Length);

                    byte[] tag = ComputeTag(result, IV_SIZE + cipherBody.Length);
                    Buffer.BlockCopy(tag, 0, result, IV_SIZE + cipherBody.Length, TAG_SIZE);

                    return result;
                }
            }
            catch (Exception e)
            {
                throw new SaveEncryptionException("Failed to encrypt save data.", e);
            }
        }

        public bool TryDecrypt(byte[] cipherText, out string plainText)
        {
            plainText = null;

            if (cipherText == null || cipherText.Length < IV_SIZE + TAG_SIZE)
                return false;

            try
            {
                int bodyLength = cipherText.Length - TAG_SIZE;

                byte[] expectedTag = ComputeTag(cipherText, bodyLength);
                if (!FixedTimeEquals(cipherText, bodyLength, expectedTag))
                    return false;

                using (var aes = Aes.Create())
                {
                    aes.Key = _encryptionKey;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    byte[] iv = new byte[IV_SIZE];
                    Buffer.BlockCopy(cipherText, 0, iv, 0, IV_SIZE);
                    aes.IV = iv;

                    using (var decryptor = aes.CreateDecryptor())
                    using (var memory = new MemoryStream(cipherText, IV_SIZE, bodyLength - IV_SIZE))
                    using (var crypto = new CryptoStream(memory, decryptor, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(crypto, Encoding.UTF8))
                    {
                        plainText = reader.ReadToEnd();
                    }
                }

                return true;
            }
            catch
            {
                // Wrong key, truncated body or bad padding — all mean "this isn't a save we can read".
                plainText = null;
                return false;
            }
        }

        byte[] ComputeTag(byte[] buffer, int length)
        {
            using (var hmac = new HMACSHA256(_macKey))
                return hmac.ComputeHash(buffer, 0, length);
        }

        /// <summary>Compares <paramref name="expected"/> against the tag stored at
        /// <paramref name="offset"/> without leaking how many bytes matched.</summary>
        static bool FixedTimeEquals(byte[] buffer, int offset, byte[] expected)
        {
            if (buffer.Length - offset != expected.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < expected.Length; i++)
                difference |= buffer[offset + i] ^ expected[i];

            return difference == 0;
        }

        static byte[] Sha256(string value)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        }
    }
}
