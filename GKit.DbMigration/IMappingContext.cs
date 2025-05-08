using System.Linq.Expressions;

namespace GKit.DbMigration;

public interface IMappingContext
{
  Task<D> DefaultAsync<D>() where D : class;
  Task<D> DefaultAsync<S, D>() where S : class where D : class;
  Task<D> FindWithSourceKeyOrDefaultAsync<D>(object oldKey, Func<Task<D>>? defaultValue = null) where D : class;
  Task<D> FindWithSourceKeyOrDefaultAsync<S, D>(object oldKey, Func<Task<D>>? defaultValue = null) where S : class where D : class;
  Task<D> FindFirstOrDefaultAsync<D>(Expression<Func<D, bool>> expression, Func<Task<D>>? defaultValue = null) where D : class;
}