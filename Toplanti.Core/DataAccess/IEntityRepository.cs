using Core.DataAccess.Collections;
using Core.Entities;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Core.DataAccess
{
    public interface IEntityRepository<T> where T : class, IEntity, new()
    {
        T Get(Expression<Func<T, bool>> filter);
        List<T> GetAll(Expression<Func<T, bool>> filter = null);
        void Add(T entity);
        void Update(T entity);
        void Delete(T entity);
        void Delete(int entityId);


        IPagedList<T> GetPagedList(Expression<Func<T, bool>> predicate = null,
                                         Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
                                         Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null,
                                         int pageIndex = 0,
                                         int pageSize = 20,
                                         bool disableTracking = true,
                                         bool ignoreQueryFilters = false);
    }
}
