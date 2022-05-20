using Core.Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Entities.DTOs
{
    public class UserStudentDto : Base
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ImagePath { get; set; }
        public string ClassName { get; set; }
        public string Email { get; set; }
        public int? CenterId { get; set; }
        public int? CountryId { get; set; }
        public string? CountryName { get; set; }
        public string? TimeZone { get; set; }
        public int? StatesId { get; set; }
        public string? StateName { get; set; }
        public int? CitiesId { get; set; }
        public string? CityName { get; set; }
        public int? GenderId { get; set; }
        public int? NationalitysId { get; set; }
        public int? OysStudentId { get; set; }
        public string ProfileImage { get; set; }
    }
}
