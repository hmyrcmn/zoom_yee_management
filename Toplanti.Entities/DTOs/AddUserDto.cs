using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Toplanti.Entities.DTOs
{
    public class AddUserDto
    {
        [Required]
        [EmailAddress]
        [JsonPropertyName("email")]
        public string? email { get; set; }

        [Required]
        [JsonPropertyName("first_name")]
        public string? first_name { get; set; }

        [JsonPropertyName("firstName")]
        public string? firstName { get; set; }

        [Required]
        [JsonPropertyName("last_name")]
        public string? last_name { get; set; }

        [JsonPropertyName("lastName")]
        public string? lastName { get; set; }

        [JsonPropertyName("password")]
        public string? password { get; set; }

        [JsonPropertyName("type")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? type { get; set; } = 1;
    }
}
