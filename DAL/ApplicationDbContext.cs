using Microsoft.EntityFrameworkCore;
using ImageSharingWithServerless.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ImageSharingWithServerless.DAL
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
    }

}