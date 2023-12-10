using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using ImageSharingWithCloud.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;

namespace ImageSharingWithCloud.DAL
{
    public class LogContext : ILogContext
    {
        protected TableClient tableClient;

        protected ILogger<LogContext> logger;

        public LogContext(IConfiguration configuration, ILogger<LogContext> logger)
        {
            this.logger = logger;
            
            /*
             * TODO Get the table service URI and table name.
             */

            var logTableUri = configuration[StorageConfig.LogEntryDbUri];
            var logTableName = configuration[StorageConfig.LogEntryDbTable];
            
            if (logTableUri == null)
            {
                throw new ArgumentNullException("Missing Log service URI in configuration: " + configuration[StorageConfig.LogEntryDbUri]);
            }

            logger.LogInformation("Looking up Storage URI... ");
            logger.LogInformation("Using Table Storage URI: " + logTableUri);
            logger.LogInformation("Using Table: " + logTableName);

            var LEDA = "bdAkD2/XcZtvlSN3+42Um6ED2E5yHuFc3YJwFjT8bz2FjutsaHUe0OmnTPjOY07oSi8VLPDtE/ed+AStXZcgBQ==";
            // Access key will have been loaded from Secrets (Development) or Key Vault (Production)
            TableSharedKeyCredential credential = new TableSharedKeyCredential(
                configuration[StorageConfig.LogEntryDbAccountName],
                LEDA);

            logger.LogInformation("Initializing table client....");
            // TODO Set the table client for interacting with the table service (see TableClient constructors)
            
            // tableClient = new TableClient(logTableUri, logTableName, credential);
            
            tableClient = new TableClient(new Uri(logTableUri), logTableName, credential);

            // tableClient.AddTableServiceClient(service);

            logger.LogInformation("....table client URI = " + tableClient.Uri);
        }


        public async Task AddLogEntryAsync(string userId, string userName, ImageView image)
        {
            LogEntry entry = new LogEntry(userId, image.Id)
            {
                Username = userName,
                Caption = image.Caption,
                ImageId = image.Id,
                Uri = image.Uri
            };

            logger.LogDebug("Adding log entry for image: {0}", image.Id);

            Response response = null;
            // TODO add a log entry for this image view

            response = await tableClient.AddEntityAsync(entry);
            
            if (response.IsError)
            {
                logger.LogError("Failed to add log entry, HTTP response {0}", response.Status);
            } 
            else
            {
                logger.LogDebug("Added log entry with HTTP response {0}", response.Status);
            }

        }

        public AsyncPageable<LogEntry> Logs(bool todayOnly = false)
        {
            if (todayOnly)
            {
                // TODO just return logs for today
                var timeStamp = DateTime.UtcNow.ToString("yyyyMMdd");
                var queryString = $"PartitionKey eq '{timeStamp}'";
                AsyncPageable<LogEntry> res = tableClient.QueryAsync<LogEntry>(queryString);
                
                return res;
            }
            else
            {
                return tableClient.QueryAsync<LogEntry>(logEntry => true);
            }
        }

    }
}