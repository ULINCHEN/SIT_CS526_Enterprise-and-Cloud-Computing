using System.ComponentModel.DataAnnotations;

namespace ImageSharingWithServerless.Models.ViewModels
{
	public class ImageViewsModel
    {
        [Required]
        [Display(Name = "Only logs for today?")]
        public bool Today { get; set; }
    }
}

