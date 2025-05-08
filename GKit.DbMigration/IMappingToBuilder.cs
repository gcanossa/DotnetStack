namespace GKit.DbMigration;

public interface IMappingToBuilder<S> where S : class
{
  IMappingMapBuilder<S, D> To<D>(Func<D, object> keySelector) where D : class;
}