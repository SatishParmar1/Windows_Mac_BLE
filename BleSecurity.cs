using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WindowsBleMesh
{
    public static class BleSecurity
    {
        // 16 bytes for AES-128
        private static readonly byte[] DefaultKey = Encoding.UTF8.GetBytes("1234567890123456"); 

        public static byte[] Encrypt(string plainText, byte[] key = null)
        {
            byte[] keyToUse = key ?? DefaultKey;
            if (keyToUse.Length != 16) throw new ArgumentException("Key must be 16 bytes for AES-128");

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyToUse;
                aes.Mode = CipherMode.ECB; // Using ECB as per spec suggestion for simplicity/size
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, null);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        return msEncrypt.ToArray();
                    }
                }
            }
        }

        public static string Decrypt(byte[] cipherText, byte[] key = null)
        {
            byte[] keyToUse = key ?? DefaultKey;
            if (keyToUse.Length != 16) throw new ArgumentException("Key must be 16 bytes for AES-128");

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyToUse;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, null);

                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}
