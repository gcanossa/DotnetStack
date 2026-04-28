using GKit.PLC;
using Microsoft.Extensions.DependencyInjection;
using S7.Net;
using S7.Net.Types;

namespace Test.Repo.PLC;

public class PlcTests
{
    [Fact]
    private void ConnectionTest()
    {
        var conn = new Plc(CpuType.S7300, "172.22.21.102", 0, 1);
        
        conn.Open();

        List<DataItem> data =
        [
            DataItem.FromAddress("DB200.DBB184"), //recipe [24]
            DataItem.FromAddress("DB200.DBB158"), //list [24]
            
            DataItem.FromAddress("DB200.DBD210"), //target_packs
            DataItem.FromAddress("DB200.DBD216"), //current_packs
            DataItem.FromAddress("DB200.DBW136"), //packs_min
            DataItem.FromAddress("DB200.DBW134"), //machine_state

            DataItem.FromAddress("DB200.DBD220"), //total_packs
            DataItem.FromAddress("DB200.DBB224"), //current_flasks
            DataItem.FromAddress("DB200.DBB228"), //total_flasks
        ];
        
        data[0].Count = 24;
        data[0].VarType = VarType.S7String;
        data[1].Count = 24;
        data[1].VarType = VarType.S7String;
        
        Assert.True(conn.IsConnected);
        
        conn.ReadMultipleVars(data);
        
        conn.Close();
    }

    [Fact]
    public async Task PlcContextTest()
    {
        var services = new ServiceCollection();
        services.AddPlcContextFactory<TestContext>(builder => builder.UseCpu(CpuType.S7300, "172.22.21.102:102, 0, 1"));
        
        using var provider = services.BuildServiceProvider();
        
        var factory = provider.GetRequiredService<IPlcContextFactory<TestContext>>();

        using var ctx = await factory.CreateContextAsync();
        
        var req = await ctx.ReadObjectAsync<PlcRequest>();
        
        Assert.NotNull(req);
    }

    public class PlcRequest
    {
        public string? Recipe { get; set; }
        public string? List { get; set; }
        public uint TargetPacks { get; set; }
        public uint CurrentPacks { get; set; }
        public uint MinPacks { get; set; }
        public uint MachineState { get; set; }
        public uint TotalPacks { get; set; }
        public uint CurrentFlasks { get; set; }
        public uint TotalFlasks { get; set; }
    }
    
    public class TestContext : PlcContext
    {
        public TestContext(IPlcContextOptions<TestContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlcRequest>()
                .Property(p => p.Recipe).ToPlcAddress("DB200.DBB184").Having(24, VarType.S7String);
            modelBuilder.Entity<PlcRequest>()
                .Property(p => p.List).ToPlcAddress("DB200.DBB158").Having(24, VarType.S7String);
            modelBuilder.Entity<PlcRequest>()
                .Property(p => p.TargetPacks).ToPlcAddress("DB200.DBD210");
            modelBuilder.Entity<PlcRequest>()
                .Property(p => p.CurrentPacks).ToPlcAddress("DB200.DBD216");
            modelBuilder.Entity<PlcRequest>()
                .Property(p => p.MinPacks).ToPlcAddress("DB200.DBW136");
            modelBuilder.Entity<PlcRequest>()
                .Property(p => p.MachineState).ToPlcAddress("DB200.DBW134");
            modelBuilder.Entity<PlcRequest>()
                .Property(p => p.TotalPacks).ToPlcAddress("DB200.DBD220");
            modelBuilder.Entity<PlcRequest>()
                .Property(p => p.CurrentFlasks).ToPlcAddress("DB200.DBB224");
            modelBuilder.Entity<PlcRequest>()
                .Property(p => p.TotalFlasks).ToPlcAddress("DB200.DBB228");
        }
    }
}