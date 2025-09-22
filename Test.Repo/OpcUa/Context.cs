using GKit.OpcUa;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Test.Repo.OpcUa;

public class Context
{
    private readonly ITestOutputHelper testOutputHelper;

    public Context(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    class TestContext : OpcUaContext
    {
        public TestContext(IOpcUaContextOptions<TestContext> options) : base(options)
        {
        }
    }
    
    [Fact]
    public async Task ShouldBeConfigured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        
        services.AddOpcUaContextFactory<TestContext>(app =>
        {
            app.ApplicationName = "Test";
        }, options =>
        {
            options.ServerUrl = "opc.tcp://localhost:62541/Quickstarts/ReferenceServer";
        });
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        
        var factory  = scope.ServiceProvider.GetService<IOpcUaContextFactory<TestContext>>();
        
        var ctx = await factory.CreateContextAsync();

        var nodes = await ctx.BrowseAsync();
        foreach (var node in nodes)
        {
            testOutputHelper.WriteLine(node.ToString());
        }
    }
}