using ImageSharingModels;
using ImageSharingWithServerless.Models;

namespace ImageSharingWithServerless.DAL
{
    public interface IImageStorage
    {
        public Task InitImageStorage();

        public Task<Image> GetImageInfoAsync(string userId, string imageId);

        public Task<IList<Image>> GetAllImagesInfoAsync();

        public Task<IList<Image>> GetImageInfoByUserAsync(ApplicationUser user);

        public Task UpdateImageInfoAsync(Image image);

        public Task SaveImageFileAsync(IFormFile imageFile, string userId, string imageId, IDictionary<string, string> metadata);

        public Task RemoveImageAsync(Image image);

        public Task RemoveImagesAsync(ApplicationUser user);

        public string ImageUri(string userId, string imageId);

        public class PendingApproval
        {
            public Image image { get; set; }
            public string messageId { get; set; }
            public string messagePopReceipt { get; set; }
        }

        public Task<IEnumerable<PendingApproval>> AwaitingApprovalAsync();

        public Task ApproveAsync(PendingApproval pending);

        public Task RejectAsync(PendingApproval pending);
    }
}
