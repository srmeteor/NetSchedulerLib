using System.IO.Compression;
using System.Text;
using Serilog;


namespace NetSchedulerLib.Utility;

public class FileOperation
    {
        private static readonly ILogger Logg = LoggerExtensions.GetLoggerFor<FileOperation>("File"); 
        
        private static readonly SemaphoreSlim 
            FileSemaphore = new SemaphoreSlim(1, 1); // For file-level operations

        private static readonly SemaphoreSlim
            DirectorySemaphore = new SemaphoreSlim(1, 1); // For directory-level operations


        public static async Task<string> ReadFileAsync(string filePath)
        {
            string fileData = string.Empty;

            try
            {
                Logg.Debug("Waiting to acquire FileSemaphore for ReadFileAsync.");
                await FileSemaphore.WaitAsync();
                Logg.Debug("Acquired FileSemaphore for ReadFileAsync.");

                if (string.IsNullOrEmpty(filePath))
                {
                    Logg.Error("Error: Empty file path.");
                    return string.Empty;
                }

                if (!File.Exists(filePath))
                {
                    Logg.Error($"Error: File \"{filePath}\" does not exist!");
                    return string.Empty;
                }

                await using var fileStream =
                    new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None, 4096, true);
                using var fileReader = new StreamReader(fileStream);
                fileData = await fileReader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                Logg.Error($"Exception in ReadFileAsync: {ex.Message}");
            }
            finally
            {
                FileSemaphore.Release();
                Logg.Debug("Released FileSemaphore for ReadFileAsync.");
            }

            return fileData;
        }

        public static async Task<bool> UpdateFileAsync(string fileData, string filePath, FileMode writeOption)
        {
            bool updateSuccess;

            await Task.Run(() => Logg.Information($"Updating file: {filePath} . . ."));

            if (string.IsNullOrEmpty(filePath))
            {
                Logg.Error("Error: Empty file info?!?");
                return false;
            }

            await FileSemaphore.WaitAsync();

            try
            {
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                await using (var fileStream =
                             new FileStream(filePath, writeOption, FileAccess.Write, FileShare.None, 4096, true))
                await using (var streamWriter = new StreamWriter(fileStream, Encoding.UTF8))
                {
                    await streamWriter.WriteAsync(fileData);
                }

                updateSuccess = true;
            }
            catch (Exception e)
            {
                updateSuccess = false;
                Logg.Error($"Updating file Exception: {e.Message}");
            }
            finally
            {
                FileSemaphore.Release();
            }

            return updateSuccess;
        }

        /// <summary>
        /// Moving Folder with its contents (overwrite destination)
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destPath"></param>
        /// <returns></returns>
        public static async Task<bool> FolderMoveAsync(string sourcePath, string destPath)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destPath))
            {
                Logg.Error("Error: Empty Source and/or Destination");
                return false;
            }

            if (!Directory.Exists(sourcePath))
            {
                Logg.Error("Error: Source Folder doesn't exist!");
                return false;
            }

            Logg.Information($"Moving folder: \"{sourcePath}\" => \"{destPath}\" . . .");

            await FileSemaphore.WaitAsync();
            try
            {
                if (Directory.Exists(destPath))
                {
                    await Task.Run(() => Directory.Delete(destPath, true));
                }

                // Use the MoveDirectoryCrossDevice helper for universal moves
                await Task.Run(() => MoveDirectoryCrossDevice(sourcePath, destPath));
                Logg.Information(
                    $"Move completed successfully from \"{sourcePath}\" to \"{destPath}\"");
                return true;
            }
            catch (Exception e)
            {
                Logg.Error($"Moving Folder Exception: {e.Message}");
                return false;
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        private static void MoveDirectoryCrossDevice(string sourceDir, string destDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDir}");
            }

            // Create the destination directory if it doesn't exist
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Copy all files from source to destination
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                if (destDir == null) continue;
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true); // Overwrite if file exists
            }

            // Recursively copy subdirectories
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                if (destDir == null) continue;
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                MoveDirectoryCrossDevice(subDir, destSubDir);
            }

            // Delete the source directory after copying
            Directory.Delete(sourceDir, true);
        }


        /// <summary>
        /// Copy Source file to destination file with overwrite option
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destPath"></param>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        public static async Task<bool> FileCopyAsync(string sourcePath, string destPath, bool overwrite)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destPath))
            {
                Logg.Error("Error: Empty Source and/or Destination file path");
                return false;
            }

            Logg.Information(
                $"Copy: \"{sourcePath}\" => \"{destPath}\" , OVERWRITE: {overwrite} . . .");

            string? directoryPath = Path.GetDirectoryName(destPath);
            if (!Directory.Exists(directoryPath))
            {
                try
                {
                    if (directoryPath != null)
                    {
                        Directory.CreateDirectory(directoryPath);
                        Logg.Information($"Created directory: {directoryPath}");
                    }
                }
                catch (Exception e)
                {
                    Logg.Error(
                        $"Failed to create directory: {directoryPath}. Exception: {e.Message}");
                    return false;
                }
            }

            await FileSemaphore.WaitAsync();
            try
            {
                File.Copy(sourcePath, destPath, overwrite);
                return true;
            }
            catch (UnauthorizedAccessException e)
            {
                Logg.Error($"Access to path denied. Exception: {e.Message}");
            }
            catch (FileNotFoundException e)
            {
                Logg.Error($"Source file not found. Exception: {e.Message}");
            }
            catch (DirectoryNotFoundException e)
            {
                Logg.Error($"Destination directory not found. Exception: {e.Message}");
            }
            catch (IOException e)
            {
                Logg.Error($"I/O error. Exception: {e.Message}");
            }
            catch (Exception e)
            {
                Logg.Error($"An unexpected error occurred. Exception: {e.Message}");
            }
            finally
            {
                FileSemaphore.Release();
            }

            return false;
        }

        public static async Task<bool> FileDeleteAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Logg.Error("Error: Empty file path");
                return false;
            }

            Logg.Information($"Deleting file: \"{filePath}\" . . .");

            await FileSemaphore.WaitAsync();
            try
            {
                await Task.Run(() => File.Delete(filePath));
                return true;
            }
            catch (Exception e)
            {
                Logg.Error($"Deleting file \"{filePath}\" Exception: {e.Message}");
                return false;
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        public static async Task<string[]?> GetFileListAsync(string directoryPath, string searchPattern)
        {
            Logg.Information(
                $"Getting files from Folder: \"{directoryPath}\" , Pattern: \"{searchPattern}\" . . .");

            if (!Directory.Exists(directoryPath))
            {
                Logg.Error("Error: No Source Directory!");
                return null;
            }

            await FileSemaphore.WaitAsync();
            try
            {
                return await Task.Run(() => string.IsNullOrEmpty(searchPattern)
                    ? Directory.GetFiles(directoryPath)
                    : Directory.GetFiles(directoryPath, searchPattern));
            }
            catch (Exception e)
            {
                Logg.Error($"Getting files Exception: {e.Message}");
                return null;
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        /// <summary>
        /// Copy Directory with all contents
        /// </summary>
        public static async Task<bool> FolderCopyAsync(string sourceFolder, string? destFolder,
            bool copySubFolders = true, bool overwrite = true, CancellationToken cancellationToken = default)
        {
            Logg.Information(
                $"Copy Folder : \"{sourceFolder}\" , and Subfolders : {copySubFolders}, overwrite: {overwrite}");

            // Only protect the setup phase (creating directories, initialization)
            await DirectorySemaphore.WaitAsync(cancellationToken);
            try
            {
                var sourceDir = new DirectoryInfo(sourceFolder);
                if (!sourceDir.Exists)
                {
                    Logg.Error("Error: No Source Folder!");
                    return false;
                }

                if (!Directory.Exists(destFolder))
                {
                    if (destFolder != null) Directory.CreateDirectory(destFolder);
                }
            }
            finally
            {
                DirectorySemaphore.Release();
            }

            // Process files and subdirectories without holding the semaphore
            try
            {
                foreach (var file in new DirectoryInfo(sourceFolder).GetFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (destFolder != null)
                    {
                        var destFilePath = Path.Combine(destFolder, file.Name);
                        if (overwrite || !File.Exists(destFilePath))
                        {
                            file.CopyTo(destFilePath, overwrite);
                        }
                    }
                }

                if (!copySubFolders) return true;
                foreach (var subDir in new DirectoryInfo(sourceFolder).GetDirectories())
                {
                    if (destFolder == null) continue;
                    var tempPath = Path.Combine(destFolder, subDir.Name);
                    await FolderCopyAsync(subDir.FullName, tempPath, true, overwrite, cancellationToken);
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                Logg.Error("Folder Copy Operation Cancelled");
                return false;
            }
            catch (Exception e)
            {
                Logg.Error($"FolderCopy Exception: {e.Message}");
                return false;
            }
        }


        /// <summary>
        /// Unzip File To
        /// </summary>
        /// <param name="zipFile">zip file complete path</param>
        /// <param name="unzipTo">where to unzip (folder path)</param>
        /// <param name="overwrite">overwrite existing files (default true)</param>
        /// <returns></returns>
        public static async Task<bool> UnzipFileAsync(string zipFile, string unzipTo, bool overwrite = true)
        {
            await FileSemaphore.WaitAsync();
            try
            {
                if (string.IsNullOrEmpty(zipFile) || string.IsNullOrEmpty(unzipTo))
                {
                    Logg.Error("Error: Empty source file path and/or destination folder path.");
                    return false;
                    
                }
                
                // Ensure the destination folder exists
                if (!Directory.Exists(unzipTo))
                {
                    Directory.CreateDirectory(unzipTo);
                }

                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(zipFile, unzipTo, overwriteFiles: overwrite);
                });
                
                Logg.Information($" * * * Unzip \"{zipFile}\" to \"{unzipTo}\" => Success",
                    true);
                return true;
            }
            catch (Exception e)
            {
                Logg.Error($"UnzipFile Exception: {e.Message}");
                return false;
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        /// <summary>
        /// Create a ZIP file from a directory (including its contents).
        /// </summary>
        /// <param name="folderPath">The directory to zip.</param>
        /// <param name="zipToFilePath">The path of the resulting ZIP file.</param>
        /// <returns>True if zipping was successful, otherwise false.</returns>
        public static async Task<bool> ZipFolderAsync(string folderPath, string zipToFilePath)
        {
            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(zipToFilePath))
            {
                Logg.Error("Error: Empty source folder and/or destination file path.");
                return false;
            }

            if (!Directory.Exists(folderPath))
            {
                Logg.Error("Error: Source directory does not exist.");
                return false;
            }

            await DirectorySemaphore.WaitAsync();
            try
            {
                // Ensure parent directory of the destination ZIP file exists.
                string? destinationDirectory = Path.GetDirectoryName(zipToFilePath);
                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                // Zip the folder.
                await Task.Run(() =>
                    ZipFile.CreateFromDirectory(folderPath, zipToFilePath, CompressionLevel.Fastest, true));
                Logg.Information(
                    $"Successfully zipped folder \"{folderPath}\" to \"{zipToFilePath}\".");
                return true;
            }
            catch (Exception ex)
            {
                Logg.Error($"ZipFolderAsync Exception: {ex.Message}");
                return false;
            }
            finally
            {
                DirectorySemaphore.Release();
            }
        }

        /// <summary>
        /// Create a ZIP file from a directory, excluding specific file types.
        /// </summary>
        /// <param name="folderPath">The directory to zip.</param>
        /// <param name="zipToFilePath">The path of the resulting ZIP file.</param>
        /// <param name="excludeExtensions">List of file extensions to exclude, e.g., [".zip", ".sgd"].</param>
        /// <returns>True if zipping was successful, otherwise false.</returns>
        public static async Task<bool> ZipFolderAsync(string folderPath, string zipToFilePath,
            List<string> excludeExtensions)
        {
            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(zipToFilePath))
            {
                Logg.Error("Error: Empty source folder and/or destination file path.");
                return false;
            }

            if (!Directory.Exists(folderPath))
            {
                Logg.Error("Error: Source directory does not exist.");
                return false;
            }

            await DirectorySemaphore.WaitAsync();
            try
            {
                // Ensure parent directory of the destination ZIP file exists.
                string? destinationDirectory = Path.GetDirectoryName(zipToFilePath);
                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                // Create the ZIP archive.
                await Task.Run(() =>
                {
                    using var zipArchive = ZipFile.Open(zipToFilePath, ZipArchiveMode.Create);
                    var dirInfo = new DirectoryInfo(folderPath);
                        
                    // Include the root folder itself in the archive
                    string rootFolderName = dirInfo.Name;
                        
                    // Add files to the ZIP, excluding specific extensions.
                    foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                    {
                        if (excludeExtensions.Contains(file.Extension.ToLower()))
                        {
                            // Skip files with excluded extensions.
                            continue;
                        }

                        // Calculate relative path with the root folder name included
                        string relativePath = Path.Combine(rootFolderName, Path.GetRelativePath(folderPath, file.FullName));

                        zipArchive.CreateEntryFromFile(file.FullName, relativePath, CompressionLevel.Fastest);

                    }
                });

                Logg.Information(
                    $"Successfully zipped folder \"{folderPath}\" to \"{zipToFilePath}\" excluding extensions: {string.Join(", ", excludeExtensions)}.");
                return true;
            }
            catch (Exception ex)
            {
                Logg.Error($"ZipFolderAsync Exception: {ex.Message}");
                return false;
            }
            finally
            {
                DirectorySemaphore.Release();
            }
        }
    }