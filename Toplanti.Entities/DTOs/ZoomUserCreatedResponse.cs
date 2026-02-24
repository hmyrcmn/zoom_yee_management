using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Toplanti.Entities.DTOs
{
    public class ZoomUserCreatedResponse
    {
        [JsonPropertyName("id")]
        public string? id { get; set; }
        [JsonPropertyName("first_name")]
        public string first_name { get; set; }
        [JsonPropertyName("last_name")]
        public string last_name { get; set; }
        [JsonPropertyName("email")]
        public string email { get; set; }
        [JsonPropertyName("type")]
        public int? type { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("password")]
        public string? password { get; set; }
    }
}
