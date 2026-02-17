using Toplanti.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Core.Entities.Concrete
{
    public class OperationClaim: Base, IEntity
    {
        public string Name { get; set; } = string.Empty;
    }
}
