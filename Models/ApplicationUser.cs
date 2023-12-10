using Microsoft.AspNetCore.Identity;

namespace ImageSharingWithServerless.Models
{
    public class ApplicationUser : IdentityUser
    {

        public virtual bool ADA { get; set; }
        public virtual bool Active { get; set; }

        public ApplicationUser()
        {
            Active = true;
            ADA = false;
        }

        public ApplicationUser(string u)
        {
            Active = true;
            UserName = u;
            Email = u;
            ADA = false;
        }

        public ApplicationUser(string u, bool isADA) 
        {
            Active = true;
            UserName = u;
            Email = u;
            ADA = isADA;
        }
    }
}
