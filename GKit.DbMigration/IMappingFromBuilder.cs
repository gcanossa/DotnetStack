namespace GKit.DbMigration;

public interface IMappingFromBuilder
{
  IMappingToBuilder<S> From<S>(Func<S, object> keySelector) where S : class;
}