namespace GKit.DbMigration;

public interface IMappingsBuilder
{
  void Mapping(Action<IMappingFromBuilder> config);
}