﻿using Microsoft.AspNetCore.Mvc;
using ImageSharingWithCloud.DAL;
using ImageSharingWithCloud.Models;

using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Azure;

namespace ImageSharingWithCloud.Controllers
{
    // TODO require authorization by default
    [Authorize]
    public class ImagesController : BaseController
    {
        protected ILogContext logContext;

        protected readonly ILogger<ImagesController> logger;

        // Dependency injection
        public ImagesController(UserManager<ApplicationUser> userManager,
                                ApplicationDbContext userContext,
                                ILogContext logContext,
                                IImageStorage imageStorage,
                                ILogger<ImagesController> logger)
            : base(userManager, imageStorage, userContext)
        {
            this.logContext = logContext;

            this.logger = logger;
        }


        // TODO
        [HttpGet]
        public ActionResult Upload()
        {
            CheckAda();

            ViewBag.Message = "";
            ImageView imageView = new ImageView();
            return View(imageView);
        }

        // TODO prevent CSRF
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Upload(ImageView imageView)
        {
            CheckAda();

            logger.LogInformation("Processing the upload of an image....");

            await TryUpdateModelAsync(imageView);

            if (!ModelState.IsValid)
            {
                ViewBag.Message = "Please correct the errors in the form!";
                return View();
            }

            logger.LogInformation("...getting the current logged-in user....");
            ApplicationUser user = await GetLoggedInUser();

            if (imageView.ImageFile == null || imageView.ImageFile.Length <= 0)
            {
                ViewBag.Message = "No image file specified!";
                return View(imageView);
            }

            logger.LogInformation("....saving image metadata in the database....");

            var imageId = Guid.NewGuid().ToString();

            // TODO save image metadata in the database 

            
            Image imageMetadata = new Image
            {
                Id = imageId,
                Caption = imageView.Caption ?? "No data",
                Description = imageView.Description ?? "No data.",
                DateTaken = DateTime.Now,
                UserId = user.Id,
                UserName = user.UserName,
                Valid = true,
                Approved = true
            };

            imageView.Id = imageId;
            logger.LogInformation("Image metadata created!");
            logger.LogInformation("imageId -> " + imageMetadata.Id);
            logger.LogInformation("Caption -> " + imageMetadata.Caption);
            logger.LogInformation("Description -> " + imageMetadata.Description);
            logger.LogInformation("DateTaken -> " + imageMetadata.DateTaken);
            logger.LogInformation("UserId -> " + imageMetadata.UserId);
            logger.LogInformation("UserId -> " + imageMetadata.UserName);
            

            var result = await imageStorage.SaveImageInfoAsync(imageMetadata);
            logger.LogInformation("Save Image metadata in cosmoDb result -> " + result);

            // end TODO

            logger.LogInformation("...saving image file on disk....");

            // TODO save image file on disk

            await imageStorage.SaveImageFileAsync(imageView.ImageFile, user.Id, imageId);

            logger.LogDebug("....forwarding to the details page, image Id = "+imageId + " && user id = " + user.Id);

            var username = user.UserName;
            await logContext.AddLogEntryAsync(user.Id, username, imageView);

            return RedirectToAction("Details", new { UserId = user.Id, Id = imageId });
        }

        // TODO
        // [HttpGet]
        // public ActionResult Query()
        // {
        //     CheckAda();
        //
        //     ViewBag.Message = "";
        //     return View();
        // }

        // TODO
        [HttpGet]
        public async Task<ActionResult> Details(string UserId, string Id)
        {
            CheckAda();
            
            logger.LogDebug("--------In Details methods --------");
            logger.LogDebug("Pass in value...");
            logger.LogDebug("UserId -> " + UserId);
            logger.LogDebug("Id -> " + Id);
            
            Image image = await imageStorage.GetImageInfoAsync(UserId, Id);
            if (image == null)
            {
                return RedirectToAction("Error", "Home", new { ErrId = "Details: " + Id });
            }

            ImageView imageView = new ImageView
            {
                Id = image.Id,
                Caption = image.Caption,
                Description = image.Description,
                DateTaken = image.DateTaken,
                UserName = image.UserName,
                UserId = image.UserId
            };
            ;

            // TODO Log this view of the image
            logger.LogInformation("Image view: UserId = {UserId}, ImageID = {ImageId}", UserId, Id);

            return View(imageView);
        }

        // TODO
        [HttpGet]
        public async Task<ActionResult> Edit(string UserId, string Id)
        {
            CheckAda();
            ApplicationUser user = await GetLoggedInUser();
            if (user == null || !user.Id.Equals(UserId))
            {
                return RedirectToAction("Error", "Home", new { ErrId = "EditNotAuth" });
            }

            Image image = await imageStorage.GetImageInfoAsync(UserId, Id);
            if (image == null)
            {
                return RedirectToAction("Error", "Home", new { ErrId = "EditNotFound" });
            }

            ViewBag.Message = "";

            ImageView imageView = new ImageView();
            imageView.Id = image.Id;
            imageView.Caption = image.Caption;
            imageView.Description = image.Description;
            imageView.DateTaken = image.DateTaken;

            imageView.UserId = image.UserId;
            imageView.UserName = image.UserName;

            return View("Edit", imageView);
        }

        // TODO prevent CSRF
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DoEdit(string UserId, string Id, ImageView imageView)
        {
            CheckAda();

            if (!ModelState.IsValid)
            {
                ViewBag.Message = "Please correct the errors on the page";
                imageView.Id = Id;
                return View("Edit", imageView);
            }

            ApplicationUser user = await GetLoggedInUser();
            if (user == null || !user.Id.Equals(UserId))
            {
                return RedirectToAction("Error", "Home", new { ErrId = "EditNotAuth" });
            }

            logger.LogDebug("Saving changes to image " + Id);
            Image image = await imageStorage.GetImageInfoAsync(imageView.UserId, Id);
            if (image == null)
            {
                return RedirectToAction("Error", "Home", new { ErrId = "EditNotFound" });
            }

            image.Caption = imageView.Caption;
            image.Description = imageView.Description;
            image.DateTaken = imageView.DateTaken;
            await imageStorage.UpdateImageInfoAsync(image);

            return RedirectToAction("Details", new { UserId = UserId, Id = Id });
        }

        // TODO
        [HttpGet]
        public async Task<ActionResult> Delete(string UserId, string Id)
        {
            CheckAda();
            ApplicationUser user = await GetLoggedInUser();
            if (user == null || !user.Id.Equals(UserId))
            {
                return RedirectToAction("Error", "Home", new { ErrId = "EditNotAuth" });
            }

            Image image = await imageStorage.GetImageInfoAsync(user.Id, Id);
            if (image == null)
            {
                return RedirectToAction("Error", "Home", new { ErrId = "EditNotFound" });
            }

            ImageView imageView = new ImageView();
            imageView.Id = image.Id;
            imageView.Caption = image.Caption;
            imageView.Description = image.Description;
            imageView.DateTaken = image.DateTaken;

            imageView.UserName = image.UserName;
            return View(imageView);
        }

        // TODO prevent CSRF
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DoDelete(string UserId, string Id)
        {
            CheckAda();
            ApplicationUser user = await GetLoggedInUser();
            if (user == null || !user.Id.Equals(UserId))
            {
                return RedirectToAction("Error", "Home", new { ErrId = "EditNotAuth" });
            }

            Image image = await imageStorage.GetImageInfoAsync(user.Id, Id);
            if (image == null)
            {
                return RedirectToAction("Error", "Home", new { ErrId = "EditNotFound" });
            }

            await imageStorage.RemoveImageAsync(image);

            return RedirectToAction("Index", "Home");

        }

        // TODO
        [HttpGet]
        public async Task<ActionResult> ListAll()
        {
            CheckAda();
            ApplicationUser user = await GetLoggedInUser();

            IList<Image> images = await imageStorage.GetAllImagesInfoAsync();
            ViewBag.UserId = user.Id;
            return View(images);
        }

        // TODO
        [HttpGet]
        public async Task<IActionResult> ListByUser()
        {
            CheckAda();

            // Return form for selecting a user from a drop-down list
            ListByUserModel userView = new ListByUserModel();
            var defaultId = (await GetLoggedInUser()).Id;

            userView.Users = new SelectList(ActiveUsers(), "Id", "UserName", defaultId);
            return View(userView);
        }

        // TODO
        [HttpGet]
        public async Task<ActionResult> DoListByUser(string Id)
        {
            CheckAda();

            // TODO list all images uploaded by the user in userView
            ApplicationUser user = await GetLoggedInUser();
            ViewBag.UserId = user.Id;
            
            var imageList = await imageStorage.GetAllImagesInfoAsync();

            IList<Image> res = new List<Image>();

             foreach (var image in imageList)
             {
                 if (image.UserId == Id) res.Add(image);
             }
            
            // 创建一个 UserView 对象，用于在视图中显示图片
            // userView userView = new UserView
            // {
            //     Id = Id,
            //     UserName = userImages.FirstOrDefault()?.UserName, 
            //     Password = null, 
            //     ADA = false 
            // };
            
            
            return View("ListAll", res);
            // End TODO

        }

        // TODO
        [HttpGet]
        public ActionResult ImageViews()
        {
            CheckAda();
            return View();
        }


        // TODO
        [HttpGet]
        public ActionResult ImageViewsList(string Today)
        {
            CheckAda();
            logger.LogDebug("Looking up log views, \"Today\"=" + Today);
            AsyncPageable<LogEntry> entries = logContext.Logs("true".Equals(Today));
            logger.LogDebug("Query completed, rendering results....");
            return View(entries);
        }

    }

}



//ListByUserModel userView = new ListByUserModel();
//IList<Image> images = await imageStorage.GetAllImagesInfoAsync();
//var userId = (await GetLoggedInUser()).Id;