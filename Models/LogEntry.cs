using Azure;
using Azure.Data.Tables;

namespace ImageSharingWithServerless.Models
{
    public class LogEntry : ITableEntity
    {
        public DateTime EntryDate { get; set; }

        public string PartitionKey { get; set; }

        public string RowKey { get; set; }

        public string Username { get; set; }

        public string Caption { get; set; }

        public string Uri { get; set; }

        public string ImageId { get; set; }
  
        public DateTimeOffset? Timestamp { get; set; }
 
        public ETag ETag { get; set; }

        public LogEntry() 
        {
            EntryDate = DateTime.UtcNow;

            PartitionKey = EntryDate.ToString("MMddyyyy");
        }

        public LogEntry(string imageId) : this()
        {
            ImageId = imageId;

            RowKey = string.Format("{0}:{1}:{2}",
                                 imageId,
                                 DateTime.MaxValue.Ticks - EntryDate.Ticks,
                                 Guid.NewGuid());
        }
    }
}
