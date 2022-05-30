using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Business.Constants
{
    public static class Messages
    {
        public static string AccessTokenCreated = "Access token başarıyla oluşturuldu";
        public static string AuthorizationDenied = "Yetkiniz yok";

        //Zoom
        public static string ZoomCreateError = "Zoom Toplantısı Oluşutulurken Hata!";
        public static string ZoomCreated = "Zoom Toplantısı Oluşutuldu.";

        public static string ZoomDeleteError = "Zoom Toplantısı Silinirken Hata!";
        public static string ZoomDeleted = "Zoom Toplantısı Silindi.";
    }
}
