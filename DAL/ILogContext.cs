using Azure;
using ImageSharingWithServerless.Models;
using ImageSharingWithServerless.Models.ViewModels;

namespace ImageSharingWithServerless.DAL
{
    /**
    * Interface for logging image views in the application.
    */
    public interface ILogContext
    {
        public Task AddLogEntryAsync(string userName, ImageView imageView);

        public AsyncPageable<LogEntry> Logs(bool todayOnly);
    }
}
