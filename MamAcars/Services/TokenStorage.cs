using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MamAcars.Services
{
    public static class TokenStorage
    {
        private static readonly string FilePath = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MamAcars", "token.dat");

        public static void SaveToken(string token)
        {
            var encryptedData = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(FilePath, encryptedData);
        }

        public static string? GetToken()
        {
            if (!File.Exists(FilePath))
                return null;

            var encryptedData = File.ReadAllBytes(FilePath);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser));
        }

        public static void DeleteToken()
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
