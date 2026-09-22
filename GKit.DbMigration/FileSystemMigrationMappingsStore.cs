using System.ComponentModel;
using System.Globalization;

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

  /// <summary>
  /// The mapping log is written by one run and read by the next, possibly on another machine.
  /// Culture-sensitive formatting would make a resumed migration re-migrate every row (or, for
  /// a Guid/decimal key, fail outright). Convert.ChangeType is avoided: it is culture-sensitive
  /// and cannot parse a Guid, a common destination key type.
  /// </summary>
  private static string FormatKey(object key) => key switch
  {
    null => "",
    string s => s,
    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
    _ => key.ToString() ?? ""
  };

  private static object ParseKey(string value, Type targetType)
  {
    if (targetType == typeof(string)) return value;

    var converter = TypeDescriptor.GetConverter(targetType);
    if (!converter.CanConvertFrom(typeof(string)))
      throw new NotSupportedException(
        $"Migration keys of type '{targetType.FullName}' cannot be read back from the mapping log.");

    return converter.ConvertFromInvariantString(value)
      ?? throw new InvalidOperationException(
        $"Could not parse '{value}' as a migration key of type '{targetType.FullName}'.");
  }

  public void Add<S, D>(object sourceKey, object destinationKey)
    where S : class
    where D : class
  {
    string mapping = GetKeyMappingKey<S, D>();

    if (!keyMappingsChanges.TryGetValue(mapping, out var item))
      keyMappingsChanges.Add(mapping, item = []);

    // Dictionary.Add's bare "An item with the same key has already been added" names neither
    // the entity nor the key, which is useless when a migration of 100k rows fails.
    if (item.ContainsKey(sourceKey) ||
        (keyMappings.TryGetValue(mapping, out var saved) && saved.ContainsKey(sourceKey)))
      throw new InvalidOperationException(
        $"A mapping from {typeof(S).Name} '{FormatKey(sourceKey)}' to {typeof(D).Name} already exists.");

    item.Add(sourceKey, destinationKey);
  }

  /// <summary>
  /// Looks in both the loaded mappings and the not-yet-saved ones.
  /// <para>
  /// The previous <c>if (!loaded.TryGetValue(..) &amp;&amp; !pending.TryGetValue(..))</c>
  /// short-circuited: once a type pair existed on disk the pending dictionary was never
  /// consulted, so a mapping added earlier in the same batch was invisible.
  /// </para>
  /// </summary>
  private bool TryLookup(string mapping, object sourceKey, out object? destinationKey)
  {
    if (keyMappings.TryGetValue(mapping, out var saved) && saved.TryGetValue(sourceKey, out destinationKey))
      return true;

    if (keyMappingsChanges.TryGetValue(mapping, out var pending) && pending.TryGetValue(sourceKey, out destinationKey))
      return true;

    destinationKey = null;
    return false;
  }

  private bool TryReverseLookup(string mapping, object destinationKey, out object? sourceKey)
  {
    foreach (var source in new[] { keyMappings, keyMappingsChanges })
    {
      if (!source.TryGetValue(mapping, out var item)) continue;

      var match = item.Where(kv => Equals(kv.Value, destinationKey)).ToList();
      if (match.Count == 0) continue;

      sourceKey = match[0].Key;
      return true;
    }

    sourceKey = null;
    return false;
  }

  private IEnumerable<string> MappingsEndingWith(Type destinationType) =>
    keyMappings.Keys.Union(keyMappingsChanges.Keys).Where(p => p.EndsWith(destinationType.FullName!));

  public bool TryGetDestinationKey<S, D>(object sourceKey, out object? destinationKey) where S : class where D : class
  {
    return TryLookup(GetKeyMappingKey<S, D>(), sourceKey, out destinationKey);
  }

  public bool TryGetSourceKey<S, D>(object destinationKey, out object? sourceKey) where S : class where D : class
  {
    return TryReverseLookup(GetKeyMappingKey<S, D>(), destinationKey, out sourceKey);
  }
  public bool TryGetDestinationKey<D>(object sourceKey, out object? destinationKey) where D : class
  {
    destinationKey = null;

    var keys = MappingsEndingWith(typeof(D)).ToList();

    // Check emptiness before ambiguity: First() on an empty sequence threw
    // InvalidOperationException before the intended ArgumentException could be reached.
    if (keys.Count == 0) return false;
    if (keys.Count > 1)
      throw new ArgumentException($"Multiple mapping found for type {typeof(D).FullName}");

    return TryLookup(keys[0], sourceKey, out destinationKey);
  }
  public bool TryGetSourceKey<D>(object destinationKey, out object? sourceKey) where D : class
  {
    sourceKey = null;

    var keys = MappingsEndingWith(typeof(D)).ToList();

    if (keys.Count == 0) return false;
    if (keys.Count > 1)
      throw new ArgumentException($"Multiple mapping found for type {typeof(D).FullName}");

    return TryReverseLookup(keys[0], destinationKey, out sourceKey);
  }

  /// <summary>
  /// Reloads the mapping log so an interrupted migration can resume.
  /// <para>
  /// Previously a directory that existed without an <c>index.txt</c> — the state left by a
  /// crash between <c>CreateDirectory</c> and the first index write — threw
  /// <see cref="FileNotFoundException"/>, making the migration permanently unresumable. A stray
  /// file with no index entry threw <see cref="KeyNotFoundException"/> for the same reason.
  /// </para>
  /// </summary>
  public Task LoadAsync()
  {
    if (!Directory.Exists(MigrationLogsPath))
      return Task.CompletedTask;

    var indexPath = Path.Combine(MigrationLogsPath, "index.txt");
    if (!File.Exists(indexPath))
      return Task.CompletedTask;

    var index = File.ReadAllLines(indexPath)
      .Select(line => line.Split(KeySeparator))
      .Where(parts => parts.Length >= 3)
      .GroupBy(parts => parts[0])
      .ToDictionary(g => g.Key, g => new Tuple<Type, Type>(Type.GetType(g.First()[1])!, Type.GetType(g.First()[2])!));

    foreach (var file in Directory.GetFiles(MigrationLogsPath).Select(p => new FileInfo(p)).Where(p => p.Name != "index.txt"))
    {
      var key = file.Name.Replace(file.Extension, "");

      if (!index.TryGetValue(key, out var types))
        continue;   // an orphan log file with no index entry: nothing can be made of it

      var map = new Dictionary<object, object>();
      keyMappings[key] = map;

      using var reader = file.OpenText();
      while (reader.ReadLine() is { } line)
      {
        if (string.IsNullOrWhiteSpace(line)) continue;

        var parts = line.Split(KeySeparator);
        if (parts.Length < 2) continue;

        map[ParseKey(parts[0], types.Item1)] = ParseKey(parts[1], types.Item2);
      }
    }

    return Task.CompletedTask;
  }

  public async Task SaveChangesAsync()
  {
    if (!Directory.Exists(MigrationLogsPath))
      Directory.CreateDirectory(MigrationLogsPath);

    // Index first: a crash between the two writes leaves log files the next LoadAsync can
    // still interpret, rather than data with no type information.
    await File.AppendAllLinesAsync(Path.Combine(MigrationLogsPath, "index.txt"), keyMappingsChanges
      .Where(kv => !keyMappings.ContainsKey(kv.Key) && kv.Value.Count > 0)
      .Select(kv => $"{kv.Key}{KeySeparator}{kv.Value.First().Key.GetType().FullName}{KeySeparator}{kv.Value.First().Value.GetType().FullName}"));

    foreach (var kv in keyMappingsChanges)
    {
      await File.AppendAllLinesAsync(Path.Combine(MigrationLogsPath, $"{kv.Key}.txt"), kv.Value
        .Select(map => $"{FormatKey(map.Key)}{KeySeparator}{FormatKey(map.Value)}"));
    }

    foreach (var kv in keyMappingsChanges)
    {
      if (!keyMappings.TryGetValue(kv.Key, out var item))
        keyMappings.Add(kv.Key, item = []);

      foreach (var map in kv.Value)
      {
        item[map.Key] = map.Value;
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