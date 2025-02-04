using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MamAcars.Utils
{
    public class FileSplitter
    {
        private const int ChunkSize = 1024 * 1024; // 1MB

        public static async Task<List<string>> SplitFileAsync(string inputFilePath, string outputFolder)
        {
            if (!File.Exists(inputFilePath))
                throw new FileNotFoundException("The specified file does not exist.", inputFilePath);

            Directory.CreateDirectory(outputFolder);

            byte[] buffer = new byte[ChunkSize];
            int chunkIndex = 1;
            List<string> chunkFiles = new();

            using FileStream inputStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read);
            int bytesRead;
            while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                string chunkFileName = Path.Combine(outputFolder, $"chunk_{chunkIndex:D4}.bin");

                await using FileStream outputStream = new FileStream(chunkFileName, FileMode.Create, FileAccess.Write);
                await outputStream.WriteAsync(buffer, 0, bytesRead);

                chunkFiles.Add(chunkFileName);
                chunkIndex++;
            }

            return chunkFiles;
        }
    }
}
