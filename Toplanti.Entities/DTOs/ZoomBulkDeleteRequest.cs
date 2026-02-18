using System.Collections.Generic;

namespace Toplanti.Entities.DTOs
{
    public class ZoomBulkDeleteRequest
    {
        public List<string> Emails { get; set; } = new();
    }
}
