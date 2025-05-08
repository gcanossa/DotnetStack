using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;

namespace GKit.DbMigration;

public abstract class DbMigrationContext<FromDbContext, ToDbContext>
  : IMappingContext, IDisposable
  where FromDbContext : DbContext
  where ToDbContext : DbContext
{
  public DbMigrationContext(
    FromDbContext oldContext, ToDbContext newContext, IMigrationMappingsStore mappingsStore, ILogger<DbMigrationContext<FromDbContext, ToDbContext>> logger)
  {
    this.fromContext = oldContext;
    this.toContext = newContext;
    this.mappingStore = mappingsStore;
    this.logger = logger;

    OnMappingEntities(new IMappingsBuilderImpl(this));
  }

  public int BatchSize { get; set; } = 100;
  private readonly FromDbContext fromContext;
  private readonly ToDbContext toContext;
  private readonly IMigrationMappingsStore mappingStore;
  private readonly ILogger<DbMigrationContext<FromDbContext, ToDbContext>> logger;

  private readonly List<IMigration> migrations = [];
  private readonly Dictionary<string, Expression> defaultMappings = [];
  private readonly Dictionary<string, object> defaultMappingsValue = [];

  public event Action<int, int>? OnMigrationStarted;
  public event Action<int>? OnMigrationProgress;

  private static string GetKeyMappingKey<S, D>()
  {
    return $"{typeof(S).FullName}_{typeof(D).FullName}";
  }

  public void Dispose()
  {
    fromContext.Dispose();
    toContext.Dispose();
    mappingStore.Dispose();

    GC.SuppressFinalize(this);
  }

  public async Task<D> DefaultAsync<D>() where D : class
  {
    var keys = defaultMappings.Keys.Where(p => p.EndsWith(typeof(D).FullName!));
    if (keys.Count() > 1)
      throw new ArgumentException($"Multiple mapping found for type {typeof(D).FullName}");

    var entityKey = keys.First();

    if (!defaultMappings.TryGetValue(entityKey, out var expression))
      throw new ArgumentException($"Default mapping not found for entity {typeof(D).Name}");

    if (defaultMappingsValue.TryGetValue(entityKey, out var entity))
    {
      toContext.Attach(entity);
      return (D)entity;
    }

    entity = await toContext.Set<D>().FirstAsync((Expression<Func<D, bool>>)expression);
    defaultMappingsValue.Add(entityKey, entity);

    return (D)entity;
  }

  public async Task<D> DefaultAsync<S, D>() where S : class where D : class
  {
    var entityKey = GetKeyMappingKey<S, D>();

    if (!defaultMappings.TryGetValue(entityKey, out var expression))
      throw new ArgumentException($"Default mapping not found for entity {typeof(D).Name}");

    if (defaultMappingsValue.TryGetValue(entityKey, out var entity))
    {
      toContext.Attach(entity);
      return (D)entity;
    }

    entity = await toContext.Set<D>().FirstAsync((Expression<Func<D, bool>>)expression);
    defaultMappingsValue.Add(entityKey, entity);

    return (D)entity;
  }

  private static async Task<D> DefaultOrError<D>(Func<Task<D>>? defaultValue) where D : class
  {
    return defaultValue is null ?
      throw new ArgumentNullException(nameof(defaultValue)) : await defaultValue.Invoke();
  }

  public async Task<D> FindWithSourceKeyOrDefaultAsync<D>(object oldKey, Func<Task<D>>? defaultValue = null) where D : class
  {
    if (!mappingStore.TryGetDestinationKey<D>(oldKey, out var key))
      return await DefaultOrError(defaultValue);

    var entity = (await toContext.FindAsync(typeof(D), key)) ?? await DefaultOrError(defaultValue);

    return (D)entity;
  }

  public async Task<D> FindWithSourceKeyOrDefaultAsync<S, D>(object oldKey, Func<Task<D>>? defaultValue = null) where S : class where D : class
  {
    if (!mappingStore.TryGetDestinationKey<S, D>(oldKey, out var key))
      return await DefaultOrError(defaultValue);

    var entity = (await toContext.FindAsync(typeof(D), key)) ?? await DefaultOrError(defaultValue);

    return (D)entity;
  }

  public async Task<D> FindFirstOrDefaultAsync<D>(Expression<Func<D, bool>> expression, Func<Task<D>>? defaultValue = null) where D : class
  {
    return (await toContext.Set<D>().FirstOrDefaultAsync(expression)) ?? await DefaultOrError(defaultValue);
  }

  protected abstract void OnMappingEntities(IMappingsBuilder builder);

  private async Task<int> CountSourceEntities<S>() where S : class
  {
    return await fromContext.Set<S>().CountAsync();
  }

  private async Task<int> CountDestinationEntities<D>() where D : class
  {
    return await toContext.Set<D>().CountAsync();
  }

  public async Task<IMappingContext> MigrateAsync()
  {
    await mappingStore.LoadAsync();

    int sourceCount = 0;
    foreach (var m in migrations)
    {
      sourceCount += await m.CountSourceAsync();
    }

    OnMigrationStarted?.Invoke(migrations.Count, sourceCount);

    foreach (var m in migrations)
    {
      await m.MigrateAsync();
    }

    return this;
  }

  private async Task MigrateEntity<Old, New>(
      Func<Old, object> oldKeySelector,
      Func<New, object> newKeySelector,
      Func<Old, IMappingContext, Task<New>> mapper,
      Expression<Func<Old, bool>> filter)
      where Old : class
      where New : class
  {
    var key = GetKeyMappingKey<Old, New>();

    var lCount = await fromContext.Set<Old>().Where(filter).CountAsync();

    logger.LogInformation($"Migrating {lCount} rows from {typeof(Old).FullName} to {typeof(New).FullName}");

    int read = 0;
    while (read < lCount)
    {
      var olds = await fromContext.Set<Old>().Where(filter).AsNoTracking().Skip(read).Take(BatchSize).ToListAsync();

      var batch = new List<Tuple<Old, New>>();
      foreach (var old in olds)
      {
        if (!mappingStore.TryGetDestinationKey<Old, New>(oldKeySelector(old), out var dkey))
        {
          var ne = await mapper(old, this);
          ne = (await toContext.Set<New>().AddAsync(ne)).Entity;
          batch.Add(new(old, ne));
        }
      }

      await toContext.SaveChangesAsync();
      foreach (var (old, ne) in batch)
      {
        mappingStore.Add<Old, New>(oldKeySelector(old), newKeySelector(ne));
      }
      batch.Clear();
      await mappingStore.SaveChangesAsync();

      logger.LogInformation($"Migrated {read}/{lCount} rows from {typeof(Old).FullName} to {typeof(New).FullName}");

      read += olds.Count;

      OnMigrationProgress?.Invoke(olds.Count);
    }

    logger.LogInformation($"Completed migrating {lCount} rows from {typeof(Old).FullName} to {typeof(New).FullName}");
  }

  private class IMappingsBuilderImpl(
    DbMigrationContext<FromDbContext, ToDbContext> ctx) : IMappingsBuilder
  {
    public void Mapping(Action<IMappingFromBuilder> config)
    {
      config(new IMappingFromBuilderImpl(ctx));
    }
  }

  private class IMappingFromBuilderImpl(
    DbMigrationContext<FromDbContext, ToDbContext> ctx) : IMappingFromBuilder
  {
    public IMappingToBuilder<S> From<S>(Func<S, object> keySelector) where S : class
    {
      return new IMappingToBuilderImpl<S>(ctx, keySelector);
    }
  }

  private class IMappingToBuilderImpl<S>(
    DbMigrationContext<FromDbContext, ToDbContext> ctx, Func<S, object> keySelector) : IMappingToBuilder<S> where S : class
  {
    public Func<S, object> KeySelector => keySelector;
    public IMappingMapBuilder<S, D> To<D>(Func<D, object> keySelector) where D : class
    {
      return new IMappingMapBuilderImpl<S, D>(ctx, KeySelector, keySelector);
    }
  }

  private class IMappingMapBuilderImpl<S, D>(
    DbMigrationContext<FromDbContext, ToDbContext> ctx, Func<S, object> fromKeySelector, Func<D, object> toKeySelector) : IMappingMapBuilder<S, D> where S : class where D : class
  {
    public Func<S, object> FromKeySelector => fromKeySelector;
    public Func<D, object> ToKeySelector => toKeySelector;

    private readonly List<D> _entities = [];
    private readonly List<Func<DbContext, Task<D>>> _entityFactories = [];
    private Expression<Func<D, bool>> _defaultSelector = p => false;
    private Expression<Func<S, bool>> _filter = p => true;

    public IMappingMapBuilder<S, D> Add(D entity)
    {
      _entities.Add(entity);
      return this;
    }
    public IMappingMapBuilder<S, D> Add(IEnumerable<D> entities)
    {
      _entities.AddRange(entities);
      return this;
    }
    public IMappingMapBuilder<S, D> AddAsync(Func<DbContext, Task<D>> factory)
    {
      _entityFactories.Add(factory);
      return this;
    }
    public IMappingMapBuilder<S, D> Default(Expression<Func<D, bool>> defaultSelector)
    {
      _defaultSelector = defaultSelector;
      return this;
    }

    public IMappingMapBuilder<S, D> Filter(Expression<Func<S, bool>> filter)
    {
      _filter = filter;
      return this;
    }

    public IMigration Map(Func<S, IMappingContext, D> mapper)
    {
      return MapAsync((f, m) => Task.FromResult(mapper(f, m)));
    }
    public IMigration MapAsync(Func<S, IMappingContext, Task<D>> mapper)
    {
      var migration = new IMappingImpl<S, D>(ctx, FromKeySelector, ToKeySelector, mapper, _filter, _entities, _entityFactories);
      ctx.migrations.Add(migration);
      if (_defaultSelector != null)
        ctx.defaultMappings.Add(GetKeyMappingKey<S, D>(), _defaultSelector);
      return migration;
    }
  }

  private class IMappingImpl<S, D>(
    DbMigrationContext<FromDbContext, ToDbContext> ctx,
    Func<S, object> fromKeySelector,
    Func<D, object> toKeySelector,
    Func<S, IMappingContext, Task<D>> mapper,
    Expression<Func<S, bool>> filter,
    IEnumerable<D> entities,
    IEnumerable<Func<DbContext, Task<D>>> entityFactories) : IMigration where S : class where D : class
  {

    public Func<S, object> FromKeySelector => fromKeySelector;
    public Func<D, object> ToKeySelector => toKeySelector;
    public Func<S, IMappingContext, Task<D>> Mapper => mapper;
    public Expression<Func<S, bool>> Filter => filter;

    public async Task<int> CountDestinationAsync()
    {
      return await ctx.CountDestinationEntities<D>();
    }

    public async Task<int> CountSourceAsync()
    {
      return await ctx.CountSourceEntities<S>();
    }

    public async Task MigrateAsync()
    {
      if (entities != null && entities.Any())
      {
        foreach (var entity in entities)
        {
          try
          {
            await ctx.toContext.AddAsync(entity);
            await ctx.toContext.SaveChangesAsync();
          }
          catch
          {
            ctx.toContext.ChangeTracker.Clear();
          }
        }
      }

      if (entityFactories != null && entityFactories.Any())
      {
        foreach (var factory in entityFactories)
        {
          try
          {
            await ctx.toContext.AddAsync(await factory.Invoke(ctx.toContext));
            await ctx.toContext.SaveChangesAsync();
          }
          catch
          {
            ctx.toContext.ChangeTracker.Clear();
          }
        }
      }


      await ctx.MigrateEntity(FromKeySelector, ToKeySelector, Mapper, Filter);
    }
  }
}