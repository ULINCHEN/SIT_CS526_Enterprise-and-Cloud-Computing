using Azure.Core;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using ImageSharingModels;
using ImageSharingWithServerless.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.EntityFrameworkCore;
using static ImageSharingWithServerless.DAL.IImageStorage;

namespace ImageSharingWithServerless.DAL
{
    public class ImageStorage : IImageStorage
    {
        protected ILogger<ImageStorage> logger;

        protected CosmosClient imageDbClient;

        protected string imageDatabase;

        protected Container imageDbContainer;

        protected BlobContainerClient blobContainerClient;


        public ImageStorage(IConfiguration configuration,
                            CosmosClient imageDbClient,
                            ILogger<ImageStorage> logger)
        {
            this.logger = logger;

            /*
             * Use Cosmos DB client to store metadata for images.
             */
            this.imageDbClient = imageDbClient;

            this.imageDatabase = configuration[StorageConfig.ImageDbDatabase];

            this.imageDbContainer = GetImageDbContainer(configuration);

            /*
             * Use Blob storage client to store images in the cloud.
             */
            this.blobContainerClient = GetBlobContainerClient(configuration);
            logger.LogInformation("ImageStorage (Blob storage) being accessed here: " + blobContainerClient.Uri);

            /*
             * Use queues for asynchronous triggers of serverless functions.
             */
            this.approvalRequestsQueueName = configuration[StorageConfig.ApprovalRequestsQueue];
            this.approvedImagesQueueName = configuration[StorageConfig.ApprovedImagesQueue];
            this.rejectedImagesQueueName = configuration[StorageConfig.RejectedImagesQueue];

            // TODO Set the queue clients for approval requests, approved and rejected images (use createQueueClient).
            // You should log the queue URIs here to ensure this has succeeded.


            this.MaxApprovalRequests = configuration.GetValue<int>(StorageConfig.MaxApprovalRequests);
            this.VisibilityTimeout = TimeSpan.FromSeconds(configuration.GetValue<int>(StorageConfig.VisibilityTimeout));
        }

        /**
         * Use this to generate the singleton Cosmos DB client that is injected into all instances of ImageStorage.
         */
        public static CosmosClient GetImageDbClient(IWebHostEnvironment environment, IConfiguration configuration)
        {
            string imageDbUri = configuration[StorageConfig.ImageDbUri];
            if (imageDbUri == null)
            {
                throw new ArgumentNullException("Missing configuration: " + StorageConfig.ImageDbUri);
            }
            string imageDbAccessKey = configuration[StorageConfig.ImageDbAccessKey];

            CosmosClientOptions cosmosClientOptions = null;
            //if (environment.IsDevelopment())
            //{
            //    cosmosClientOptions = new CosmosClientOptions()
            //    {
            //        HttpClientFactory = () =>
            //        {
            //            HttpMessageHandler httpMessageHandler = new HttpClientHandler()
            //            {
            //                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            //            };

            //            return new HttpClient(httpMessageHandler);
            //        },
            //        ConnectionMode = ConnectionMode.Gateway
            //    };
            //}

            CosmosClient imageDbClient = new CosmosClient(imageDbUri, imageDbAccessKey, cosmosClientOptions);
            return imageDbClient;
        }

        /**
         * Use this to generate the Cosmos DB container client
         */
        private Container GetImageDbContainer(IConfiguration configuration)
        {
            string imageContainer = configuration[StorageConfig.ImageDbContainer];
            logger.LogDebug("ImageDb (Cosmos DB) is being accessed here: " + imageDbClient.Endpoint);
            logger.LogDebug("ImageDb using database {0} and container {1}",
                imageDatabase, imageContainer);
            Database imageDbDatabase = imageDbClient.GetDatabase(imageDatabase);
            return imageDbDatabase.GetContainer(imageContainer);
        }

        /**
         * Use this to generate the blob container client
         */
        private static BlobContainerClient GetBlobContainerClient(IConfiguration configuration)
        {
            string imageStorageUriFromConfig = configuration[StorageConfig.ImageStorageUri];
            if (imageStorageUriFromConfig == null)
            {
                throw new ArgumentNullException("Missing Blob service URI in configuration: " + StorageConfig.ImageStorageUri);
            }
            Uri imageStorageUri = new Uri(imageStorageUriFromConfig);

            StorageSharedKeyCredential credential = null;
            // TODO get the shared key credential for accessing blob storage.


            BlobServiceClient blobServiceClient = new BlobServiceClient(imageStorageUri, credential, null);

            string storageContainer = configuration[StorageConfig.ImageStorageContainer];
            if (storageContainer == null)
            {
                throw new ArgumentNullException("Missing Blob container name in configuration: " + StorageConfig.ImageStorageContainer);
            }
            return blobServiceClient.GetBlobContainerClient(storageContainer);

        }


        /*
         * Initialize image database and queues.
         */
        public async Task InitImageStorage()
        {
            logger.LogInformation("Initializing image storage (Cosmos DB and Blob Storage)....");
            await imageDbClient.CreateDatabaseIfNotExistsAsync(imageDatabase);
            await blobContainerClient.CreateIfNotExistsAsync();
            IList<Image> images = await GetAllImagesInfoAsync();
            foreach (Image image in images)
            {
                await RemoveImageAsync(image);
            }
            logger.LogInformation("....initialization completed.");


            var queueResponse = await approvalRequests.CreateIfNotExistsAsync();
            if (queueResponse != null)
            {
                logger.LogInformation("Confirmed approval request queue: {0}", this.approvalRequestsQueueName);
            }

            queueResponse = await approvedImages.CreateIfNotExistsAsync();
            if (queueResponse != null)
            {
                logger.LogInformation("Confirmed approved requests queue: {0}", this.approvedImagesQueueName);
            }

            queueResponse = await rejectedImages.CreateIfNotExistsAsync();
            if (queueResponse != null)
            {
                logger.LogInformation("Confirmed rejected requests queue: {0}", this.rejectedImagesQueueName);
            }

            await approvalRequests.ClearMessagesAsync();
            await approvedImages.ClearMessagesAsync();
            await rejectedImages.ClearMessagesAsync();
        }


        public async Task<Image> GetImageInfoAsync(string userId, string imageId)
        {
            return await imageDbContainer.ReadItemAsync<Image>(imageId, new PartitionKey(userId));
        }

        public async Task<IList<Image>> GetAllImagesInfoAsync()
        {
            List<Image> results = new List<Image>();
            var iterator = imageDbContainer.GetItemLinqQueryable<Image>()
                                           .Where(im => im.Valid && im.Approved)
                                           .ToFeedIterator();
            // Iterate over the paged query result.
            while (iterator.HasMoreResults)
            {
                var images = await iterator.ReadNextAsync();
                // Iterate over a page in the query result.
                foreach (Image image in images)
                {
                    results.Add(image);
                }
            }
            return results;
        }

        public async Task<IList<Image>> GetImageInfoByUserAsync(ApplicationUser user)
        {
            List<Image> results = new List<Image>();
            var query = imageDbContainer.GetItemLinqQueryable<Image>()
                                        .WithPartitionKey<Image>(user.Id)
                                        .Where(im => im.Valid && im.Approved);
            // TODO complete this

            return results;
        }

        public async Task UpdateImageInfoAsync(Image image)
        {
            await imageDbContainer.ReplaceItemAsync<Image>(image, image.Id, new PartitionKey(image.UserId));
        }

        /*
         * Remove both image files and their metadata records in the database.
         */
        public async Task RemoveImagesAsync(ApplicationUser user)
        {
            var query = imageDbContainer.GetItemLinqQueryable<Image>().WithPartitionKey<Image>(user.Id);
            var iterator = query.ToFeedIterator();
            while (iterator.HasMoreResults)
            {
                var images = await iterator.ReadNextAsync();
                foreach (Image image in images)
                {
                    await RemoveImageAsync(image);
                }
            }
            /*
             * Not available?
             * await imageDbContainer.DeleteAllItemsByPartitionKeyStreamAsync(new PartitionKey(image.UserId))
             */
        }

        public async Task RemoveImageAsync(Image image)
        {
            try
            {
                await RemoveImageFileAsync(image);
            }
            catch (Azure.RequestFailedException e)
            {
                logger.LogError("Exception while removing blob image: ", e.StackTrace);
            }
            await imageDbContainer.DeleteItemAsync<Image>(image.Id, new PartitionKey(image.UserId));
        }


        /**
         * The name of a blob containing a saved image (imageId is key for metadata record).
         */
        protected static string BlobName(string userId, string imageId)
        {
            // return "image-" + imageId + ".jpg";
            return imageId;
        }

        protected string BlobUri(string userId, string imageId)
        {
            return blobContainerClient.Uri + "/" + BlobName(userId, imageId);
        }

        public Task SaveImageFileAsync(IFormFile imageFile, string userId, string imageId, IDictionary<string, string> metadata)
        {
            logger.LogInformation("Saving image with id {0} to blob storage", imageId);

            BlobHttpHeaders headers = new BlobHttpHeaders();
            headers.ContentType = "image/jpeg";

            /*
             * TODO upload data to blob storage
             * 
             * Tip: You need to reset the stream position to the beginning before uploading:
             * See https://stackoverflow.com/a/47611795.
             * 
             * Do NOT await the finishing of the upload!
             */

            return Task.CompletedTask;

        }

        protected async Task RemoveImageFileAsync(Image image)
        {
            BlobClient blobClient = blobContainerClient.GetBlobClient(BlobName(image.UserId, image.Id));
            logger.LogInformation("Deleting image blob at URI {0}", blobClient.Uri);
            await blobClient.DeleteAsync();
        }

        public string ImageUri(string userId, string imageId)
        {
            return BlobUri(userId, imageId);
        }

        private bool isOkImage(Image image)
        {
            return image.Valid && image.Approved;
        }

        /*
         * API for image approval.
         */

        // https://learn.microsoft.com/en-us/azure/storage/queues/storage-dotnet-how-to-use-queues?tabs=dotnet

        // https://learn.microsoft.com/en-us/dotnet/api/azure.storage.queues.queueclient?view=azure-dotnet


        protected string approvalRequestsQueueName;

        protected string approvedImagesQueueName;

        protected string rejectedImagesQueueName;

        protected QueueClient approvalRequests;

        protected QueueClient approvedImages;

        protected QueueClient rejectedImages;

        /*
         * Maximum number of requests to be processed in one go (should be in app settings)
         */
        protected int MaxApprovalRequests;

        /*
         * How long approval request messages are invisible while they are being processed (should be in app settings)
         */
        protected TimeSpan VisibilityTimeout;

        private static QueueClient createQueueClient(Uri queueUri, string queueName, string accountName, string accessKey)
        {
            /*
             * Queue when reading messages expects Base64 encoding for payload, but does not
             * automatically encode with Base64 when sending, so set it here as default behavior.
             */
            string connectionString = $"DefaultEndpointsProtocol=http;AccountName={accountName};AccountKey={accessKey};QueueEndpoint={queueUri}";
            QueueClient client = new QueueClient(connectionString, queueName, new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            });
            return client;
        }

        /*
         * Get a list of images awaiting approval (from the approval-requests queue).
         */
        public async Task<IEnumerable<PendingApproval>> AwaitingApprovalAsync()
        {
            QueueMessage[] messages = await approvalRequests.ReceiveMessagesAsync(MaxApprovalRequests, VisibilityTimeout);
            logger.LogDebug($"Getting list of approval requests from message queue ({messages.Length} messages)....");
            List<PendingApproval> pending = new List<PendingApproval>();
            foreach (QueueMessage message in messages)
            {
                Image image = ImageProperties.messageTextToImage(message.MessageText);
                logger.LogDebug($"...approval request: {image.Uri}...");
                PendingApproval pendingApproval = new PendingApproval()
                {
                    image = image,
                    messageId = message.MessageId,
                    messagePopReceipt = message.PopReceipt
                };
                pending.Add(pendingApproval);
            }
            return pending;
        }

        /*
         * An image is approved: remove it from the approval-requests queue and add it to the approved-images queue.
         */
        public async Task ApproveAsync(PendingApproval pending)
        {
            logger.LogDebug($"Image has been approved: {pending.image.Uri}");
            string messageText = ImageProperties.imageToMessageText(pending.image);
            await approvedImages.SendMessageAsync(messageText);
            await approvalRequests.DeleteMessageAsync(pending.messageId, pending.messagePopReceipt);
        }

        /*
         * An image is rejected: remove it from the approval-requests queue and add it to the rejected-images queue.
         */
        public async Task RejectAsync(PendingApproval pending)
        {
            logger.LogDebug($"Image has been rejected: {pending.image.Uri}");
            // TODO remove from requests queue and add to rejects queue

        }


    }
}
