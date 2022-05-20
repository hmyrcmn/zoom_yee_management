using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public interface IEntity
    {
        public int Id { get; set; }
        public DateTime AddedTime { get; set; }
        public DateTime ChangedTime { get; set; }
        public bool Active { get; set; }
        public bool Deleted { get; set; }
    }
}
