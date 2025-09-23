using System.Text.Json;
using System.Text.Json.Serialization;
using GKit.OpcUa;
using Microsoft.Extensions.DependencyInjection;
using Opc.Ua;
using Xunit.Abstractions;

namespace Test.Repo.OpcUa;

public class ContextTest
{
    private readonly ITestOutputHelper testOutputHelper;

    public ContextTest(ITestOutputHelper testOutputHelper)
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

        services.AddOpcUaContextFactory<TestContext>(builder =>
            builder.WithApplication(
                "opc.tcp://localhost:62541/Quickstarts/ReferenceServer",
                async (client, provider) => client.SetClientOperationLimits(new OperationLimits())
                .SetDefaultSessionTimeout(60000)
                .WithDefaultWellKnownDiscoveryUrls()
                .WithDynamicSelfSignedApplicationCertificate(builder.DefaultApplicationUri(), builder.DefaultApplicationName(), "localhost")
            ).AcceptUntrustedCertificates());
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        
        var factory  = scope.ServiceProvider.GetService<IOpcUaContextFactory<TestContext>>();
        
        var ctx = await factory.CreateContextAsync();

        Assert.NotNull(ctx);
    }
    
    [Fact(Skip = "Debug only")]
    public async Task ShouldBrowseServer()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddOpcUaContextFactory<TestContext>(builder =>
            builder.WithApplication(
                "opc.tcp://localhost:62541/Quickstarts/ReferenceServer",
                async (client, provider) => client.SetClientOperationLimits(new OperationLimits())
                .SetDefaultSessionTimeout(60000)
                .WithDefaultWellKnownDiscoveryUrls()
                .WithDynamicSelfSignedApplicationCertificate(builder.DefaultApplicationUri(), builder.DefaultApplicationName(), "localhost")
            ).AcceptUntrustedCertificates());
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        
        var factory  = scope.ServiceProvider.GetService<IOpcUaContextFactory<TestContext>>();
        
        var ctx = await factory.CreateContextAsync();

        var nodes = await ctx.BrowseAsync();
        foreach (var node in nodes)
        {
            testOutputHelper.WriteLine($"DisplayName = {{0}}, NodeClass = {{1}}", node.DisplayName, node.NodeClass);
        }

        var variables = nodes.Where(p => p.NodeClass == NodeClass.Variable)
            .Select(p => new { DisplayName = p.DisplayName, Read = new ReadValueId()
                { NodeId = new NodeId(p.NodeId.ToString()), AttributeId = Attributes.Value }}).ToList();
        
        var readVariables = await ctx.ReadNodesAsync(variables.Select(p => p.Read));

        testOutputHelper.WriteLine("");

        var i = 0;
        foreach (var variable in readVariables)
        {
            testOutputHelper.WriteLine($"ValueDisplayName = {{0}}, Value = {{1}}", 
                variables[i].DisplayName, 
                JsonSerializer.Serialize(variable.Value, new JsonSerializerOptions()
                {
                    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
                }));
            i++;
        }
    }
    
    [Fact]
    public async Task ShouldWriteData()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddOpcUaContextFactory<TestContext>(builder =>
            builder.WithApplication(
                "opc.tcp://localhost:62541/Quickstarts/ReferenceServer",
                async (client, provider) => client.SetClientOperationLimits(new OperationLimits())
                .SetDefaultSessionTimeout(60000)
                .WithDefaultWellKnownDiscoveryUrls()
                .WithDynamicSelfSignedApplicationCertificate(builder.DefaultApplicationUri(), builder.DefaultApplicationName(), "localhost")
            ).AcceptUntrustedCertificates());
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        
        var factory  = scope.ServiceProvider.GetService<IOpcUaContextFactory<TestContext>>();
        
        var ctx = await factory.CreateContextAsync();
        
        var readVariables = await ctx.ReadNodesAsync([new ReadValueId()
        {
            NodeId = new NodeId("ns=2;s=Scalar_Static_Int32"),
            AttributeId = Attributes.Value
        }]);

        var value = (int)readVariables.First().Value + 1;
        
        var writeResults = await ctx.WriteNodesAsync([new WriteValue
        {
            NodeId = new NodeId("ns=2;s=Scalar_Static_Int32"),
            AttributeId = Attributes.Value,
            Value = new DataValue { Value = value }
        }]);
        
        Assert.True(StatusCode.IsGood(writeResults.First().Code));
        
        readVariables = await ctx.ReadNodesAsync([new ReadValueId()
        {
            NodeId = new NodeId("ns=2;s=Scalar_Static_Int32"),
            AttributeId = Attributes.Value
        }]);

        Assert.Equal(value, readVariables.First().Value);
    }
}