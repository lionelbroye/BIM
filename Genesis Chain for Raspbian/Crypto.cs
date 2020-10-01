using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Diagnostics;
using System.Numerics;
using firstchain.arith256;
using System.Threading;
using System.Timers;

namespace firstchain
{
    public partial class Program
    {
        // SHA256 Hash algorythm

        public static byte[] ComputeSHA256(byte[] msg)
        {
            SHA256 sha = SHA256.Create();
            byte[] result = sha.ComputeHash(msg);
            return result;
        }
        public static string SHAToHex(byte[] bytes, bool upperCase)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));

            return result.ToString();
        }
        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        // Generating assymetric RSA keys (4096 bits) method

        public static void GenerateNewPairKey() // better to use offline & on another device. 
        {
            if (File.Exists(_folderPath + "privateKey") || File.Exists(_folderPath + "publicKey"))
            {
                Print("Already existing RSA key files has been found in app folder. Please move them or rename them. RSA Key Gen has been aborted");
                return;
            }
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(4096);
            byte[] _privateKey = rsa.ExportCspBlob(true);
            byte[] _publicKey = rsa.ExportCspBlob(false);
            File.WriteAllBytes(_folderPath + "privateKey", rsa.ExportCspBlob(true));
            File.WriteAllBytes(_folderPath + "publicKey", rsa.ExportCspBlob(false));
            rsa.Clear();
            if ( File.Exists(_folderPath + "QRMYKEYS.exe"))
            {
                Print("RSA public and private keys successfully created and saved in app folder! ");
                Process.Start(_folderPath + "QRMYKEYS.exe");
                Print("QR Code of your assymetric keys will be generated... ");
                // Print("Please (4 security) use QRMYKEYS, print output and delete key files ");

            }


        }
    }
}
