using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace GKit.DbMigration;

public interface IMappingMapBuilder<S, D> where S : class where D : class
{
  IMappingMapBuilder<S, D> Add(D entity);
  IMappingMapBuilder<S, D> Add(IEnumerable<D> entities);
  IMappingMapBuilder<S, D> AddAsync(Func<DbContext, Task<D>> factory);

  IMappingMapBuilder<S, D> Default(Expression<Func<D, bool>> defaultSelector);
  IMappingMapBuilder<S, D> Filter(Expression<Func<S, bool>> filter);

  IMigration Map(Func<S, IMappingContext, D> mapper);
  IMigration MapAsync(Func<S, IMappingContext, Task<D>> mapper);
}