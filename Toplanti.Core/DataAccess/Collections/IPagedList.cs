using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Core.DataAccess.Collections
{
    public interface IPagedList<T>
    {
        int IndexFrom { get; }
        int PageIndex { get; }
        int PageSize { get; }
        int TotalPages { get; }
        int TotalRecords { get; }
        IList<T> Items { get; }
        bool HasPreviousPage { get; }
        bool HasNextPage { get; }
    }
}
