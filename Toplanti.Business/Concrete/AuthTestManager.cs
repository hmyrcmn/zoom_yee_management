using Toplanti.Business.Abstract;
using Toplanti.Business.BusinessAspects.Autofac;
using Toplanti.Core.Utilities.Results;

namespace Toplanti.Business.Concrete
{
    public class AuthTestManager : IAuthTestService
    {
        [SecuredOperation("Admin")]
        public IResult AdminOnlyTest()
        {
            return new SuccessResult("You have Admin access.");
        }
    }
}
