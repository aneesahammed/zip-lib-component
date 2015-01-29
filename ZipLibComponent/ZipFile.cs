using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;

namespace ZipLibComponent
{
    public sealed class ZipFile
    {
        private const string EXT = ".gzip";

        #region <-PublicMethods->

        /// <summary>
        /// Compress file/folder
        /// </summary>
        /// <param name="path">file/folder path to compress</param>
        /// <param name="isFile">path is file/folder[default=false]</param>
        /// <returns>0 if success else -1</returns>
        public Windows.Foundation.IAsyncOperation<int> CompressAsync(string path, bool isFile)
        {
            return CompressAsyncHelper(path, "", isFile).AsAsyncOperation();
        }


        /// <summary>
        /// Compress file/folder
        /// </summary>
        /// <param name="path">file/folder path to compress</param>
        /// <param name="outFileName">.gzip file[default= empty]</param>
        /// <param name="isFile">path is file/folder[default=false]</param>
        /// <returns>0 if success else -1</returns>
        public Windows.Foundation.IAsyncOperation<int> CompressAsync(string path, string outFileName, bool isFile)
        {
            return CompressAsyncHelper(path, outFileName, isFile).AsAsyncOperation();
        }

        /// <summary>
        /// Extracts given .gzip file
        /// </summary>
        /// <param name="filePath">.gzip file path to be extracted</param>
        /// <returns>0 if success else -1</returns>
        public Windows.Foundation.IAsyncOperation<int> ExtractAsync(string filePath)
        {
            return ExtractFileAsyncHelper(filePath).AsAsyncOperation();
        }

        #endregion

        #region <-PrivateMethods->

        private static async System.Threading.Tasks.Task<int> CompressAsyncHelper(string path, string outFileName = "",
                                                                                  bool isFile = false)
        {
            try
            {
                if (!String.IsNullOrEmpty(path))
                {
                    //isFile = IsFileAsync(path).Result;

                    if (String.IsNullOrEmpty(outFileName))
                    {
                        outFileName = String.Format("{0}{1}",
                                                    isFile
                                                        ? System.IO.Path.GetFileNameWithoutExtension(path)
                                                        : System.IO.Path.GetFileName(path), EXT);
                    }
                    else
                    {
                        if (!outFileName.EndsWith(".gzip", StringComparison.Ordinal))
                            outFileName = String.Format("{0}{1}", outFileName, EXT);
                    }

                    StorageFolder inputStorageFolder =
                        await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(path));
                    if (inputStorageFolder != null)
                    {
                        StorageFile outFile =
                            await
                            inputStorageFolder.CreateFileAsync(outFileName, CreationCollisionOption.GenerateUniqueName);

                        if (outFile != null)
                            using (var outFileStream = await outFile.OpenAsync(FileAccessMode.ReadWrite))
                            {
                                using (
                                    var gzipStream = new GZipStream(outFileStream.AsStreamForWrite(),
                                                                    CompressionMode.Compress))
                                {
                                    if (isFile)
                                    {
                                        var stFileToCompress = await StorageFile.GetFileFromPathAsync(path);

                                        if (stFileToCompress != null)
                                            await CompressFileAsync(stFileToCompress, gzipStream);
                                    }
                                    else
                                    {
                                        var stFolderToCompress = await StorageFolder.GetFolderFromPathAsync(path);
                                        if (stFolderToCompress != null)
                                        {
                                            var files =
                                                await stFolderToCompress.GetFilesAsync(CommonFileQuery.OrderByName);

                                            foreach (var stFileToCompress in files)
                                            {
                                                await CompressFileAsync(stFileToCompress, gzipStream);
                                            }
                                        }
                                    }
                                }
                            }
                    }
                }
                else
                    return -1;
            }
            catch (Exception)
            {
                return -1;
            }
            return 0;
        }

        private static async System.Threading.Tasks.Task CompressFileAsync(StorageFile storageFile,
                                                                           GZipStream gzipStream)
        {
            char[] chars = storageFile.Name.ToCharArray();
            gzipStream.Write(BitConverter.GetBytes(chars.Length), 0, sizeof (int));
            foreach (var c in chars)
            {
                gzipStream.Write(BitConverter.GetBytes(c), 0, sizeof (char));
            }

            var buffer = await Windows.Storage.FileIO.ReadBufferAsync(storageFile);
            gzipStream.Write(BitConverter.GetBytes(buffer.Length), 0, sizeof (int));

            var bytes = new byte[buffer.Length];
            DataReader dataReader = DataReader.FromBuffer(buffer);
            dataReader.ReadBytes(bytes);
            gzipStream.Write(bytes, 0, bytes.Length);
        }


        private static async System.Threading.Tasks.Task<int> ExtractFileAsyncHelper(string filePath)
        {
            try
            {
                if (!String.IsNullOrEmpty(filePath))
                {
                    var stFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(filePath));
                    var stFileToDecompress = await stFolder.GetFileAsync(Path.GetFileName(filePath));

                    //************************************
                    var outDirectory =
                        await
                        stFolder.CreateFolderAsync(stFileToDecompress.DisplayName,
                                                   CreationCollisionOption.GenerateUniqueName);
                    //************************************

                    using (var stFileStreamToDecompress = await stFileToDecompress.OpenReadAsync())
                    {
                        using (
                            var gzipStream = new GZipStream(stFileStreamToDecompress.AsStreamForRead(),
                                                            CompressionMode.Decompress))
                        {
                            while (await ExtractFileAsync(gzipStream, outDirectory))
                            {
                            }
                        }
                    }
                }
                else
                    return -1;
            }
            catch (Exception)
            {
                return -1;
            }
            return 0;
        }

        private static async System.Threading.Tasks.Task<bool> ExtractFileAsync(GZipStream gzipStream,
                                                                                StorageFolder outDirectory)
        {
            var bufferFileNameLength = new byte[sizeof (int)];

            int readed = gzipStream.Read(bufferFileNameLength, 0, sizeof (int));
            if (readed < sizeof (int))
                return false;

            var fileNameLength = BitConverter.ToInt32(bufferFileNameLength, 0);

            var bufferFileName = new byte[sizeof (char)];
            var sb = new StringBuilder();
            for (int i = 0; i < fileNameLength; i++)
            {
                gzipStream.Read(bufferFileName, 0, sizeof (char));
                char c = BitConverter.ToChar(bufferFileName, 0);
                sb.Append(c);
            }

            var fileName = sb.ToString();

            var bufferFileContentLength = new byte[sizeof (int)];
            gzipStream.Read(bufferFileContentLength, 0, bufferFileContentLength.Length);
            var fileContentLength = BitConverter.ToInt32(bufferFileContentLength, 0);

            var bufferFileContent = new byte[fileContentLength];
            gzipStream.Read(bufferFileContent, 0, bufferFileContent.Length);


            var tempFile = await outDirectory.CreateFileAsync(fileName);
            using (var tempFileStream = await tempFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                Stream stream = tempFileStream.AsStreamForWrite();
                await stream.WriteAsync(bufferFileContent, 0, fileContentLength);
                await stream.FlushAsync();
            }

            return true;
        }

        #region Utility

        private async System.Threading.Tasks.Task<bool> IsFileAsync(string path)
        {
            var isFile = false;
            if (!String.IsNullOrEmpty(path))
            {
                try
                {
                    await StorageFile.GetFileFromPathAsync(path);
                    isFile = true;
                }
                catch (Exception)
                {
                    isFile = false;
                }
            }
            return isFile;
        }

        #endregion

        #endregion
    }
}
