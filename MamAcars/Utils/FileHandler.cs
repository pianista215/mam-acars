using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace MamAcars.Utils
{
    public static class FileHandler
    {
        public static void WriteToFile(string filePath, string content)
        {
            File.WriteAllText(filePath, content);
        }

        public static void CompressFile(string inputFilePath, string outputFilePath)
        {
            using var inputFileStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read);
            using var outputFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            using var gzipStream = new GZipStream(outputFileStream, CompressionLevel.Optimal);

            inputFileStream.CopyTo(gzipStream);
        }

        public static string GenerateSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            var hashBytes = sha256.ComputeHash(stream);
            return Convert.ToBase64String(hashBytes);
        }
    }
}
