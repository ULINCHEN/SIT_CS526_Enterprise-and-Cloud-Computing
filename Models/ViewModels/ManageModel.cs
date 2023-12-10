using Microsoft.AspNetCore.Mvc.Rendering;

namespace ImageSharingWithServerless.Models.ViewModels
{
    public class ManageModel
    {
        public IList<SelectListItem> Users { get; set; }

    }
}
