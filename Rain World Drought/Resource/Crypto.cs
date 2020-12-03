using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// https://stackoverflow.com/questions/5251759/easy-way-to-encrypt-obfuscate-a-byte-array-using-a-secret-in-net
/// </summary>
namespace Rain_World_Drought.Resource
{
    public static class Crypto
    {
        /*
        public static void DumpString(string value)
        {
            Debug.Log(value);
            foreach (char c in value)
            {
                Debug.Log(string.Concat( "{0:x4} ", (int)c ));
            }
        }*/

        /// <summary>
        /// Create and initialize a crypto algorithm.
        /// </summary>
        /// <param name="password">The password.</param>
        private static SymmetricAlgorithm GetAlgorithm(int password)
        {
            var algorithm = Rijndael.Create();
            var rdb = new Rfc2898DeriveBytes(password.GetHashCode().ToString(), new byte[] {
        0x53,0x6f,0x64,0x69,0x75,0x6d,0x20,             // salty goodness
        0x43,0x68,0x6c,0x6f,0x72,0x69,0x64,0x65
    });
            algorithm.Padding = PaddingMode.ISO10126;
            algorithm.Key = rdb.GetBytes(32);
            algorithm.IV = rdb.GetBytes(16);
            return algorithm;
        }

        /// <summary>
        /// Encrypts a string with a given password.
        /// </summary>
        /// <param name="clearText">The clear text.</param>
        /// <param name="password">The password.</param>
        public static string EncryptStringAES(string clearText, int password)
        {
            using (SymmetricAlgorithm algorithm = GetAlgorithm(password))
            {
                ICryptoTransform encryptor = algorithm.CreateEncryptor();
                byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
                //return Convert.ToBase64String(clearBytes);

                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(clearBytes, 0, clearBytes.Length);
                    cs.Close();

                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        /// <summary>
        /// Decrypts a string using a given password.
        /// </summary>
        /// <param name="cipherText">The cipher text.</param>
        /// <param name="password">The password.</param>
        public static string DecryptStringAES(string cipherText, int password)
        {
            using (SymmetricAlgorithm algorithm = GetAlgorithm(password))
            {
                ICryptoTransform decryptor = algorithm.CreateDecryptor();

                byte[] cipherBytes = Convert.FromBase64String(cipherText);

                //return Encoding.Unicode.GetString(cipherBytes);

                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                {
                    cs.Write(cipherBytes, 0, cipherBytes.Length);
                    cs.Close();

                    return Encoding.Unicode.GetString(ms.ToArray());
                }
            }
        }
    }
}
