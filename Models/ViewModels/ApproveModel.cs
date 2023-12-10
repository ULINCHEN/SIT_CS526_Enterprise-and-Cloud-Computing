using Microsoft.AspNetCore.Mvc.Rendering;

namespace ImageSharingWithServerless.Models.ViewModels
{
    public class ApproveModel
    { 
        public IList<SelectListItem> Images
        {
            get; set;
        }
    }
}
