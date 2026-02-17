using Toplanti.Core.Utilities.Results;

namespace Toplanti.Business.Abstract
{
    public interface IAuthTestService
    {
        IResult AdminOnlyTest();
    }
}
