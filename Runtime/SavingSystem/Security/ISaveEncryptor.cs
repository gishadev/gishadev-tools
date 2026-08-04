using System;

namespace gishadev.tools.SavingSystem
{
    /// <summary>Turns save JSON into opaque bytes and back.
    /// <para>Encrypting fails loudly so a broken cipher can never be written over a good save;
    /// decrypting fails quietly so the store can fall back to an empty save instead.</para></summary>
    public interface ISaveEncryptor
    {
        /// <exception cref="SaveEncryptionException">The payload could not be encrypted.</exception>
        byte[] Encrypt(string plainText);

        /// <summary>Returns false for a wrong key, tampered or truncated data.</summary>
        bool TryDecrypt(byte[] cipherText, out string plainText);
    }

    public class SaveEncryptionException : Exception
    {
        public SaveEncryptionException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }
}
