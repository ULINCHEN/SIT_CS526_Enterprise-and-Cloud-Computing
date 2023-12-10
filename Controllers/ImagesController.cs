using Microsoft.AspNetCore.Mvc;
using ImageSharingWithServerless.DAL;
using ImageSharingModels;
using ImageSharingWithServerless.Models;
using ImageSharingWithServerless.Models.ViewModels;

using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Azure;
using Newtonsoft.Json;
using static ImageSharingWithServerless.DAL.IImageStorage;

namespace ImageSharingWithServerless.Controllers
{
    // TODO require authorization by default
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

        public ActionResult Upload()
        {
            CheckAda();

            ViewBag.Message = "";
            ImageView imageView = new ImageView();
            return View(imageView);
        }

        // TODO prevent CSRF

        public async Task<ActionResult> Upload(ImageView imageView)
        {
            CheckAda();

            logger.LogDebug("Processing the upload of an image....");

            await TryUpdateModelAsync(imageView);

            if (!ModelState.IsValid)
            {
                ViewBag.Message = "Please correct the errors in the form!";
                return View();
            }

            logger.LogDebug("...getting the current logged-in user....");
            ApplicationUser user = await GetLoggedInUser();

            if (imageView.ImageFile == null || imageView.ImageFile.Length <= 0)
            {
                ViewBag.Message = "No image file specified!";
                return View(imageView);
            }

            string imageId = Guid.NewGuid().ToString();

            /*
             * We will attach metadata for image to upload to blob storage.
             * Once upload is finished, image metadata will be saved in Cosmos DB.
             */
            IDictionary<string, string> metadata = new Dictionary<string, string>();
            metadata[ImageProperties.USER_KEY] = user.Id;
            metadata[ImageProperties.ID_KEY] = imageId;
            metadata[ImageProperties.USERNAME_KEY] = user.UserName;
            metadata[ImageProperties.CAPTION_KEY] = imageView.Caption;
            metadata[ImageProperties.DESCRIPTION_KEY] = imageView.Description;
            DateTime dateTakenUtc = DateTime.SpecifyKind(imageView.DateTaken, DateTimeKind.Utc);
            metadata[ImageProperties.DATE_TAKEN_KEY] = JsonConvert.SerializeObject(dateTakenUtc);
            metadata[ImageProperties.URL_KEY] = imageStorage.ImageUri(user.Id, imageId);

            logger.LogDebug("...saving image file to blob storage....");

            // TODO Save image file on disk
            // 1. Be sure to include metadata, which will be processed by Azure function
            // 2. No need to await finish of upload! (catch and log exceptions on upload thread)
            // Because this call is not awaited, execution of the current method continues before the call is completed


            ViewBag.Message = "Image uploaded!";
            return View(new ImageView());
        }

        // TODO

        public ActionResult Query()
        {
            CheckAda();

            ViewBag.Message = "";
            return View();
        }

        // TODO

        public async Task<ActionResult> Details(string UserId, string Id)
        {
            CheckAda();

            Image image = await imageStorage.GetImageInfoAsync(UserId, Id);
            if (image == null)
            {
                return RedirectToAction("Error", "Home", new { ErrId = "Details: " + Id });
            }

            ImageView imageView = new ImageView();
            imageView.Id = image.Id;
            imageView.Caption = image.Caption;
            imageView.Description = image.Description;
            imageView.DateTaken = image.DateTaken;
            imageView.Uri = imageStorage.ImageUri(image.UserId, image.Id);

            imageView.UserName = image.UserName;
            imageView.UserId = image.UserId;

            string thisUser = this.User.Identity.Name;
            // TODO Log this view of the image asynchronously
            // Because this call is not awaited, execution of the current method continues before the call is completed


            return View(imageView);
        }

        // TODO

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

        public async Task<ActionResult> ListAll()
        {
            CheckAda();
            ApplicationUser user = await GetLoggedInUser();

            IList<Image> images = await imageStorage.GetAllImagesInfoAsync();
            ViewBag.UserId = user.Id;
            return View(images);
        }

        // TODO

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

        public async Task<ActionResult> DoListByUser(string Id)
        {
            CheckAda();

            // TODO list all images uploaded by the user in userView

            return null;
            // End TODO

        }

        // TODO

        public ActionResult ImageViews()
        {
            CheckAda();
            return View();
        }

        // TODO

        public ActionResult ImageViewsList(string Today)
        {
            CheckAda();
            logger.LogDebug("Looking up log views, \"Today\"=" + Today);
            AsyncPageable<LogEntry> entries = logContext.Logs("true".Equals(Today));
            logger.LogDebug("Query completed, rendering results....");
            return View(entries);
        }


        /*
         * Image Approval actions.
         */


        public async Task<IActionResult> Approve()
        {
            CheckAda();

            logger.LogDebug("Retrieving approval requests....");
            var pendingApprovals = await imageStorage.AwaitingApprovalAsync();
            List<SelectListItem> items = new List<SelectListItem>();
            foreach (var pendingApproval in pendingApprovals)
            {
                string uri = pendingApproval.image.Uri;
                string json = JsonConvert.SerializeObject(pendingApproval);
                SelectListItem item = new SelectListItem { Text = uri, Value = json, Selected = false };
                items.Add(item);
            }

            ViewBag.message = "";
            ApproveModel model = new ApproveModel { Images = items };
            return View(model);
        }


        public async Task<IActionResult> Approve(ApproveModel model)
        {
            CheckAda();

            foreach (var item in model.Images)
            {
                PendingApproval pending = JsonConvert.DeserializeObject<PendingApproval>(item.Value);
                if (item.Selected)
                {
                    await imageStorage.ApproveAsync(pending);
                }
                else
                {
                    await imageStorage.RejectAsync(pending);
                }
            }

            ViewBag.message = "Images approved!";

            return View(new ApproveModel { Images = new List<SelectListItem>() }); ;
        }

    }

}
