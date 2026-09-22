using System.Net;
using GKit.PLC;
using S7.Net;
using S7.Net.Types;

namespace GKit.Tests.PLC;

public class PlcModelBuildingTests
{
  private sealed class FakeOptions : IPlcContextOptions
  {
    public CpuType CpuType { get; set; } = CpuType.S71500;
    public IPAddress Address { get; set; } = IPAddress.Loopback;
    public int Port { get; set; } = 102;
    public short Rack { get; set; } = 0;
    public short Slot { get; set; } = 1;
  }

  private sealed class Status
  {
    public uint MinPacks { get; set; }
    public uint TargetPacks { get; set; }
    public State MachineState { get; set; }
    public string? Recipe { get; set; }
    public long Unmappable { get; set; }
  }

  public enum State { Idle, Running }

  private sealed class TestContext : PlcContext
  {
    // PlcContext calls OnModelCreating from its own constructor, i.e. before any derived
    // instance state is usable. A static hand-off is the only unambiguous way to get the
    // configuration in; the tests in this class run sequentially.
    [ThreadStatic] private static Action<IModelBuilder>? _pending;

    private TestContext(IPlcContextOptions options) : base(options) { }

    public static TestContext Create(Action<IModelBuilder> configure)
    {
      _pending = configure;
      try { return new TestContext(new FakeOptions()); }
      finally { _pending = null; }
    }

    protected override void OnModelCreating(IModelBuilder modelBuilder)
      => _pending!.Invoke(modelBuilder);
  }

  private static DataItem DataItemFor(TestContext context, string propertyName) =>
    context.EntityModels[typeof(Status)].Single(kv => kv.Key.Name == propertyName).Value.DataItem;

  [Fact]
  public void A_typed_address_determines_the_width_not_the_clr_type()
  {
    // "DBW" is two bytes. A uint property previously overwrote that with DWord and read four,
    // silently spilling into whatever followed in the data block.
    using var context = TestContext.Create(b =>
      b.Entity<Status>().Property(p => p.MinPacks).ToPlcAddress("DB200.DBW136"));

    var item = DataItemFor(context, nameof(Status.MinPacks));

    Assert.Equal(VarType.Word, item.VarType);
    Assert.Equal(136, item.StartByteAdr);
    Assert.Equal(200, item.DB);
  }

  [Fact]
  public void A_doubleword_address_stays_a_doubleword()
  {
    using var context = TestContext.Create(b =>
      b.Entity<Status>().Property(p => p.TargetPacks).ToPlcAddress("DB200.DBD210"));

    Assert.Equal(VarType.DWord, DataItemFor(context, nameof(Status.TargetPacks)).VarType);
  }

  [Fact]
  public void An_enum_property_at_a_word_address_reads_a_word()
  {
    // The enum's underlying type is int (DInt, four bytes); the address says two. The address
    // describes the PLC, so it wins.
    using var context = TestContext.Create(b =>
      b.Entity<Status>().Property(p => p.MachineState).ToPlcAddress("DB200.DBW134"));

    Assert.Equal(VarType.Word, DataItemFor(context, nameof(Status.MachineState)).VarType);
  }

  [Fact]
  public void Having_overrides_the_address_derived_type()
  {
    using var context = TestContext.Create(b =>
      b.Entity<Status>().Property(p => p.Recipe).ToPlcAddress("DB200.DBB184").Having(24, VarType.S7String));

    var item = DataItemFor(context, nameof(Status.Recipe));

    Assert.Equal(VarType.S7String, item.VarType);
    Assert.Equal(24, item.Count);
  }

  [Fact]
  public void A_property_with_no_determinable_type_fails_the_model_build()
  {
    var ex = Assert.Throws<InvalidOperationException>(() =>
      TestContext.Create(b =>
        b.Entity<Status>().Property(p => p.Unmappable).ToDb(1).WithCoordinates(0, 0)));

    Assert.Contains(nameof(Status.Unmappable), ex.Message);
  }

  [Fact]
  public void A_clr_typed_property_without_an_address_uses_the_inferred_type()
  {
    using var context = TestContext.Create(b =>
      b.Entity<Status>().Property(p => p.TargetPacks).ToDb(1).WithCoordinates(4, 0));

    Assert.Equal(VarType.DWord, DataItemFor(context, nameof(Status.TargetPacks)).VarType);
  }
}
