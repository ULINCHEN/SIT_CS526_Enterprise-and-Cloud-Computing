using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageSharingWithCloud.Models;

namespace ImageSharingModels
{
    /*
     * Properties provided as metadata when an image is uploaded.
     */
    public static class ImageProperties
    {
        public const string URL_KEY = "url";

        public const string USER_KEY = "user";

        public const string ID_KEY = "id";

        public const string USERNAME_KEY = "username";

        public const string CAPTION_KEY = "caption";

        public const string DESCRIPTION_KEY = "description";

        public const string DATE_TAKEN_KEY = "date-taken";


        /*
         * Helper functions
         */
        public static Image messageTextToImage(string messageText)
        {
            // Image object is JSON string in message payload
            return JsonConvert.DeserializeObject<Image>(messageTextToString(messageText));
        }

        public static string messageTextToString(string messageText)
        {
            return Encoding.UTF8.GetString(messageTextToBytes(messageText));
        }

        public static byte[] messageTextToBytes(string messageText)
        {
            // Message payload is base64-encoded by Azure Queue.
            return Convert.FromBase64String(messageText);
        }

        public static string imageToMessageText(Image image)
        {
            string json = JsonConvert.SerializeObject(image);
            byte[] data = Encoding.UTF8.GetBytes(json);
            return Convert.ToBase64String(data);
        }

    }
}