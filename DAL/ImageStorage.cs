using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ImageSharingWithCloud.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.EntityFrameworkCore;

namespace ImageSharingWithCloud.DAL
{
    public class ImageStorage : IImageStorage
    {
        protected ILogger<ImageStorage> logger; // 日志

        protected CosmosClient imageDbClient; // consmos clinet

        protected string imageDatabase; 

        protected Container imageDbContainer; // 图片 metadata 存放

        protected BlobContainerClient blobContainerClient; // 图片文件存储


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
            string imageContainer = configuration[StorageConfig.ImageDbContainer];
            logger.LogInformation("ImageDb (Cosmos DB) is being accessed here: " + imageDbClient.Endpoint);
            logger.LogInformation("ImageDb using database {0} and container {1}",
                imageDatabase, imageContainer);
            Database imageDbDatabase = imageDbClient.GetDatabase(imageDatabase);
            this.imageDbContainer = imageDbDatabase.GetContainer(imageContainer);

            /*
             * Use Blob storage client to store images in the cloud.
             */
            string imageStorageUriFromConfig = configuration[StorageConfig.ImageStorageUri];
            if (imageStorageUriFromConfig == null)
            {
                throw new ArgumentNullException("Missing Blob service URI in configuration: " + StorageConfig.ImageStorageUri);
            }
            Uri imageStorageUri = new Uri(imageStorageUriFromConfig);



            // TODO get the shared key credential for accessing blob storage.
            
            //Console.WriteLine("ImageStorageAccessKey:" + configuration[StorageConfig.ImageStorageAccessKey]);
            //Console.WriteLine("ImageStorageAccessKey:" + configuration[StorageConfig.ImageStorageAccessKey]);

            var ISAN = "youlinchen";
            var ISAK = "bdAkD2/XcZtvlSN3+42Um6ED2E5yHuFc3YJwFjT8bz2FjutsaHUe0OmnTPjOY07oSi8VLPDtE/ed+AStXZcgBQ==";

            StorageSharedKeyCredential credential = new StorageSharedKeyCredential(ISAN, ISAK);
            //StorageSharedKeyCredential credential = new StorageSharedKeyCredential( configuration[StorageConfig.ImageStorageAccountName], configuration[StorageConfig.ImageStorageAccessKey]);
            BlobServiceClient blobServiceClient = new BlobServiceClient(imageStorageUri, credential, null);

            string storageContainer = configuration[StorageConfig.ImageStorageContainer];
            if (storageContainer == null)
            {
                throw new ArgumentNullException("Missing Blob container name in configuration: " + StorageConfig.ImageStorageContainer);
            }
            this.blobContainerClient = blobServiceClient.GetBlobContainerClient(storageContainer);
            logger.LogInformation("ImageStorage (Blob storage) being accessed here: " + blobContainerClient.Uri);
        }

        /**
         * Use this to generate the singleton Cosmos DB client that is injected into all instances of ImageStorage.
         */
        public static CosmosClient GetImageDbClient(IWebHostEnvironment environment, IConfiguration configuration)
        {
            string imageDbUri = configuration[StorageConfig.ImageDbUri];
            if (imageDbUri == null)
            {
                throw new ArgumentNullException("Missing configuration: " + configuration[StorageConfig.ImageDbUri]);
            }
            string imageDbAccessKey = configuration[StorageConfig.ImageDbAccessKey];

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions();
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
            var key = "MQG3TkGUO4jfDfhQWQsYJkdEjEL9fFboASf4F1eBjpg1UdPdeJynRDu4Iz4rrOdKGt7ByGFajvcNACDbWTo65g==";
            CosmosClient imageDbClient = new CosmosClient(imageDbUri, key, cosmosClientOptions);
            return imageDbClient;
        }

        /*
         * 
         */
        public async Task InitImageStorage()
        {

            logger.LogInformation("Initializing image storage (Cosmos DB)....");
            await imageDbClient.CreateDatabaseIfNotExistsAsync(imageDatabase);
            logger.LogInformation("....initialization completed.");
        }

        /*
         * Save image metadata in the database.
         */
        public async Task<string> SaveImageInfoAsync(Image image)
        {
            image.Id = image.Id;
            // TODO
            
            logger.LogInformation("Saving Image info in cosmoDB...");
            logger.LogInformation("Image id -> " + image.Id);
            logger.LogInformation("Partition Key is user id -> " + image.UserId);
            
            var res = await imageDbContainer.CreateItemAsync<Image>(image, new PartitionKey(image.UserId));
            
            logger.LogInformation("Saving image info process finished.");
            
            return res.ToString();

        }

        public async Task<Image> GetImageInfoAsync(string userId, string imageId)
        {
            var res = await imageDbContainer.ReadItemAsync<Image>(imageId, new PartitionKey(userId));
            return res;
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
            var iterator = query.ToFeedIterator();

            while (iterator.HasMoreResults)
            {
                FeedResponse<Image> response = await iterator.ReadNextAsync();

                foreach (var item in response)
                {
                    results.Add(item);
                }
            }

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
                await imageDbContainer.DeleteItemAsync<Image>(image.Id, new PartitionKey(image.UserId));
            }
            catch (Azure.RequestFailedException e)
            {
                logger.LogError("Exception while removing blob image: ", e.StackTrace);
            }
        }


        /**
         * The name of a blob containing a saved image (imageId is key for metadata record).
         */
        protected static string BlobName(string userId, string imageId)
        {
            return "image-" + imageId + ".jpg";
        }

        protected string BlobUri(string userId, string imageId)
        {
            return blobContainerClient.Uri + "/" + BlobName(userId, imageId);
        }

        public async Task SaveImageFileAsync(IFormFile imageFile, string userId, string imageId)
        {
            logger.LogInformation("Saving image with id {0} to blob storage", imageId);

            BlobHttpHeaders headers = new BlobHttpHeaders();
            headers.ContentType = "image/jpeg";

            /*
             * TODO upload data to blob storage
             * 
             * Tip: You need to reset the stream position to the beginning before uploading:
             * See https://stackoverflow.com/a/47611795.
             */
            
            
            logger.LogInformation("Start upload image file to blob storage...");
            await using (var stream = imageFile.OpenReadStream())
            {
                stream.Seek(0, SeekOrigin.Begin); 
                
                await blobContainerClient.UploadBlobAsync(BlobName(userId, imageId), stream);
            }
            logger.LogInformation("Image file upload process finished!");
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

        // private bool isOkImage(Image image)
        // {
        //     return image.Valid && image.Approved;
        // }
    }
}