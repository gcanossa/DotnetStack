namespace GKit.DbMigration;

public interface IMigration
{
  Task<int> CountSourceAsync();
  Task<int> CountDestinationAsync();

  Task MigrateAsync();
}