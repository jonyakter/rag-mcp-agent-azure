using Azure.Storage.Blobs;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MyExperiment
{
    /// <summary>
    /// Handles the Azure Blob operations: downloading the input dataset for the
    /// experiment and uploading the produced result file(s).
    /// </summary>
    public class AzureBlobStorageProvider
    {
        private readonly BlobContainerClient _trainingContainer;
        private readonly BlobContainerClient _resultContainer;
        private readonly string _trainingContainerName;

        public AzureBlobStorageProvider(string connectionString, string trainingContainer, string resultContainer)
        {
            _trainingContainerName = trainingContainer;

            _trainingContainer = new BlobContainerClient(connectionString, trainingContainer);
            _trainingContainer.CreateIfNotExists();

            _resultContainer = new BlobContainerClient(connectionString, resultContainer);
            _resultContainer.CreateIfNotExists();
        }

        /// <summary>
        /// Downloads the input file from the training container to a local temp file
        /// and returns the local path.
        /// </summary>
        public async Task<string> DownloadInputAsync(string fileName)
        {
            // Extract blob path
            string blobName = ExtractBlobName(fileName, _trainingContainerName);

            // Get blob client
            BlobClient blob = _trainingContainer.GetBlobClient(blobName);

            // Create unique temp path
            string localPath = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}_{Path.GetFileName(blobName)}");

            await blob.DownloadToAsync(localPath);
            return localPath;
        }

        /// <summary>
        /// Uploads a local result file to the result container. Returns the blob name.
        /// </summary>
        public async Task<string?> UploadResultFileAsync(string experimentName, string localFilePath)
        {
            if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
                return null;

            string blobName = $"{experimentName}_{Path.GetFileName(localFilePath)}";
            BlobClient blob = _resultContainer.GetBlobClient(blobName);

            await blob.UploadAsync(localFilePath, overwrite: true);
            return blobName;
        }

        /// <summary>
        /// If fileName is a full URL, returns the blob name after the container segment;
        /// otherwise returns fileName unchanged.
        /// </summary>
        private static string ExtractBlobName(string fileName, string containerName)
        {
            // Check empty value
            if (string.IsNullOrWhiteSpace(fileName))
                return fileName;

            // Check for full URL
            if (Uri.TryCreate(fileName, UriKind.Absolute, out var uri))
            {
                // Get URL path
                string path = uri.AbsolutePath.TrimStart('/');

                // Build container prefix
                string prefix = containerName + "/";

                // Remove container prefix
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return path.Substring(prefix.Length);
                }

                // Return file name only
                return Path.GetFileName(path);
            }

            return fileName;
        }
    }
}
