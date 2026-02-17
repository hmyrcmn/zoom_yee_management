using Toplanti.Core.DataAccess.EntityFramework;
using Toplanti.Core.Entities.Concrete;
using Toplanti.DataAccess.Abstract;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;

namespace Toplanti.DataAccess.Concrete.EntityFramework
{
    public class EfOperationClaimDal : EfEntityRepositoryBase<OperationClaim, ToplantiContext>, IOperationClaimDal
    {
    }
}
