using Microsoft.AspNetCore.Mvc.Rendering;

namespace ImageSharingWithServerless.Models.ViewModels
{
    public class ListByUserModel
    {
        public string Id { get; set; }
        public IEnumerable<SelectListItem> Users { get; set; }
    }
}
