
namespace GKit.DbMigration;

public class FileSystemMigrationMappingsStore : IMigrationMappingsStore
{
  public string MigrationLogsPath { get; set; } = "./.migration-logs";
  public string KeySeparator { get; set; } = "|";

  private readonly Dictionary<string, Dictionary<object, object>> keyMappings = [];

  private readonly Dictionary<string, Dictionary<object, object>> keyMappingsChanges = [];

  private static string GetKeyMappingKey<S, D>()
  {
    return $"{typeof(S).FullName}_{typeof(D).FullName}";
  }

  public void Add<S, D>(object sourceKey, object destinationKey)
    where S : class
    where D : class
  {
    string mapping = GetKeyMappingKey<S, D>();

    if (!keyMappingsChanges.TryGetValue(mapping, out var item))
      keyMappingsChanges.Add(mapping, item = []);

    item.Add(sourceKey, destinationKey);
  }

  public bool TryGetDestinationKey<S, D>(object sourceKey, out object? destinationKey) where S : class where D : class
  {
    string mapping = GetKeyMappingKey<S, D>();

    destinationKey = null;

    if (!keyMappings.TryGetValue(mapping, out var item) && !keyMappingsChanges.TryGetValue(mapping, out item))
      return false;

    if (!item.TryGetValue(sourceKey, out destinationKey))
      return false;

    return true;
  }

  public bool TryGetSourceKey<S, D>(object destinationKey, out object? sourceKey) where S : class where D : class
  {
    string mapping = GetKeyMappingKey<S, D>();

    sourceKey = null;

    if (!keyMappings.TryGetValue(mapping, out var item) && !keyMappingsChanges.TryGetValue(mapping, out item))
      return false;

    var result = item.Where(kv => kv.Value == destinationKey).ToList();

    if (result.Count == 0)
      return false;

    sourceKey = result[0].Key;
    return true;
  }
  public bool TryGetDestinationKey<D>(object sourceKey, out object? destinationKey) where D : class
  {
    var keys = keyMappings.Keys.Union(keyMappingsChanges.Keys).Where(p => p.EndsWith(typeof(D).FullName!));
    if (keys.Count() > 1)
      throw new ArgumentException($"Multiple mapping found for type {typeof(D).FullName}");

    destinationKey = null;

    if (!keys.Any())
      return false;

    var mapping = keys.First();

    if (!keyMappings.TryGetValue(mapping, out var item) && !keyMappingsChanges.TryGetValue(mapping, out item))
      return false;

    if (!item.TryGetValue(sourceKey, out destinationKey))
      return false;

    return true;

  }
  public bool TryGetSourceKey<D>(object destinationKey, out object? sourceKey) where D : class
  {
    var keys = keyMappings.Keys.Union(keyMappingsChanges.Keys).Where(p => p.EndsWith(typeof(D).FullName!));
    if (keys.Count() > 1)
      throw new ArgumentException($"Multiple mapping found for type {typeof(D).FullName}");

    sourceKey = null;

    if (!keys.Any())
      return false;
    var mapping = keys.First();

    if (!keyMappings.TryGetValue(mapping, out var item) && !keyMappingsChanges.TryGetValue(mapping, out item))
      return false;

    var result = item.Where(kv => kv.Value == destinationKey).ToList();

    if (result.Count == 0)
      return false;

    sourceKey = result[0].Key;
    return true;
  }

  public Task LoadAsync()
  {
    if (!Directory.Exists(MigrationLogsPath))
      return Task.CompletedTask;

    Dictionary<string, Tuple<Type, Type>> index = File.ReadAllLines(Path.Combine(MigrationLogsPath, "index.txt"))
      .Select(line => line.Split(KeySeparator))
      .ToDictionary(parts => parts[0], parts => new Tuple<Type, Type>(Type.GetType(parts[1])!, Type.GetType(parts[2])!));

    foreach (var file in Directory.GetFiles(MigrationLogsPath).Select(p => new FileInfo(p)).Where(p => p.Name != "index.txt"))
    {
      var map = new Dictionary<object, object>();
      var key = file.Name.Replace(file.Extension, "");
      keyMappings.Add(key, map);

      var types = index[key];

      using var reader = file.OpenText();
      while (reader.ReadLine() is var line && line is not null)
      {
        var parts = line.Split(KeySeparator);
        map.Add(Convert.ChangeType(parts[0], types.Item1), Convert.ChangeType(parts[1], types.Item2));
      }
    }

    return Task.CompletedTask;
  }

  public async Task SaveChangesAsync()
  {
    if (!Directory.Exists(MigrationLogsPath))
      Directory.CreateDirectory(MigrationLogsPath);

    await File.WriteAllLinesAsync(Path.Combine(MigrationLogsPath, "index.txt"), keyMappingsChanges
      .Where(kv => !keyMappings.ContainsKey(kv.Key))
      .Select(kv => $"{kv.Key}{KeySeparator}{kv.Value.First().Key.GetType().FullName}{KeySeparator}{kv.Value.First().Value.GetType().FullName}"));

    foreach (var kv in keyMappingsChanges)
    {
      await File.WriteAllLinesAsync(Path.Combine(MigrationLogsPath, $"{kv.Key}.txt"), kv.Value
        .Select(map => $"{map.Key}{KeySeparator}{map.Value}"));
    }

    foreach (var kv in keyMappingsChanges)
    {
      if (!keyMappings.TryGetValue(kv.Key, out var item))
        keyMappings.Add(kv.Key, item = []);

      foreach (var map in kv.Value)
      {
        item.Add(map.Key, map.Value);
      }
    }

    keyMappingsChanges.Clear();
  }

  public void Dispose()
  {
    keyMappings.Clear();
    keyMappingsChanges.Clear();

    GC.SuppressFinalize(this);
  }
}