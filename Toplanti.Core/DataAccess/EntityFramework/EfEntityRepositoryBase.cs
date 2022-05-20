using Core.DataAccess.Collections;
using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Core.DataAccess.EntityFramework
{
    public class EfEntityRepositoryBase<TEntity, TContext> : IEntityRepository<TEntity>
    where TEntity : class, IEntity, new()
    where TContext : DbContext, new()
    {
        public void Add(TEntity entity)
        {
            using (var context = new TContext())
            {
                entity.Active = true;
                entity.Deleted = false;
                entity.AddedTime = DateTime.Now;
                entity.ChangedTime = DateTime.Now;

                var addedEntity = context.Entry(entity);
                addedEntity.State = EntityState.Added;
                context.SaveChanges();
            }
        }

        public void Delete(TEntity entity)
        {
            using (var context = new TContext())
            {
                entity.Active = false;
                entity.Deleted = true;
                entity.ChangedTime = DateTime.Now;

                var deletedEntity = context.Entry(entity);
                deletedEntity.State = EntityState.Modified;
                context.SaveChanges();
            }
        }

        public void Delete(int entityId)
        {
            using (var context = new TContext())
            {
                TEntity entity = context.Set<TEntity>().SingleOrDefault(s => s.Id == entityId);
                entity.Active = false;
                entity.Deleted = true;
                entity.ChangedTime = DateTime.Now;

                var deletedEntity = context.Entry(entity);
                deletedEntity.State = EntityState.Modified;
                context.SaveChanges();
            }
        }

        public TEntity Get(Expression<Func<TEntity, bool>> filter)
        {
            using (var context = new TContext())
            {
                return context.Set<TEntity>().SingleOrDefault(filter);
            }
        }

        public List<TEntity> GetAll(Expression<Func<TEntity, bool>> filter = null)
        {
            using (var context = new TContext())
            {
                return filter == null
                    ? context.Set<TEntity>().ToList()
                    : context.Set<TEntity>().Where(filter).ToList();
            }
        }

        public void Update(TEntity entity)
        {
            using (var context = new TContext())
            {
                entity.ChangedTime = DateTime.Now;
                var updatedEntity = context.Entry(entity);
                updatedEntity.State = EntityState.Modified;
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Gets the <see cref="IPagedList{TEntity}"/> based on a predicate, orderby delegate and page information. This method default no-tracking query.
        /// </summary>
        /// <param name="predicate">A function to test each element for a condition.</param>
        /// <param name="orderBy">A function to order elements.</param>
        /// <param name="include">A function to include navigation properties</param>
        /// <param name="pageIndex">The index of page.</param>
        /// <param name="pageSize">The size of the page.</param>
        /// <param name="disableTracking"><c>True</c> to disable changing tracking; otherwise, <c>false</c>. Default to <c>true</c>.</param>
        /// <param name="ignoreQueryFilters">Ignore query filters</param>
        /// <returns>An <see cref="IPagedList{TEntity}"/> that contains elements that satisfy the condition specified by <paramref name="predicate"/>.</returns>
        /// <remarks>This method default no-tracking query.</remarks>
        public virtual IPagedList<TEntity> GetPagedList(Expression<Func<TEntity, bool>> predicate = null,
                                                Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
                                                Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> include = null,
                                                int pageIndex = 0,
                                                int pageSize = 20,
                                                bool disableTracking = true,
                                                bool ignoreQueryFilters = false)
        {
            using (var context = new TContext())
            {
                IQueryable<TEntity> query = context.Set<TEntity>();

                if (disableTracking)
                {
                    query = query.AsNoTracking();
                }

                if (include != null)
                {
                    query = include(query);
                }

                if (predicate != null)
                {
                    query = query.Where(predicate);
                }

                if (ignoreQueryFilters)
                {
                    query = query.IgnoreQueryFilters();
                }

                if (orderBy != null)
                {
                    return orderBy(query).ToPagedList(pageIndex, pageSize);
                }
                else
                {
                    return query.ToPagedList(pageIndex, pageSize);
                }
            }
        }
    }
}
