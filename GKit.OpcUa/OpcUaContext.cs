using System.Runtime.CompilerServices;
using Opc.Ua;
using Opc.Ua.Client;

namespace GKit.OpcUa;

public abstract class OpcUaContext
{
    protected IOpcUaContextOptions Options { get; init; }

    public OpcUaConnection Connection => OpcUaConnectionPool.Connections.GetValueOrDefault(Options)
                                         ?? throw new InvalidOperationException("Connection not found");

    public OpcUaContext(IOpcUaContextOptions options)
    {
        Options = options;

        OpcUaConnectionPool.Connections.TryAdd(Options, new OpcUaConnection(options));
    }

    protected async Task EnsureConnected(CancellationToken ct = default)
    {
        var connected = await Connection.ConnectAsync(ct).ConfigureAwait(false);
        if (!connected) throw new InvalidOperationException("Connection failed");
    }

    public async Task<IEnumerable<DataValue>> ReadNodesAsync(IEnumerable<ReadValueId> nodes,CancellationToken ct = default)
    {
        await EnsureConnected(ct).ConfigureAwait(false);

        // build a list of nodes to be read
        var nodesToRead = new ReadValueIdCollection(nodes);

        // Call Read Service
        var response = await Connection.Session!.ReadAsync(
            null,
            0,
            TimestampsToReturn.Both,
            nodesToRead,
            ct).ConfigureAwait(false);

        var resultsValues = response.Results;

        // Validate the results
        ClientBase.ValidateResponse(resultsValues,nodesToRead);

        return resultsValues;
    }

    /// <summary>
    /// Write a list of nodes to the Server.
    /// </summary>
    public async Task<IEnumerable<StatusCode>> WriteNodesAsync(IEnumerable<WriteValue> values, CancellationToken ct = default)
    {
        await EnsureConnected(ct).ConfigureAwait(false);

        // Write the configured nodes
        var nodesToWrite = new WriteValueCollection(values);

        // Call Write Service
        var response = await Connection.Session.WriteAsync(
            null,
            nodesToWrite,
            ct).ConfigureAwait(false);

        var results = response.Results;

        // Validate the response
        ClientBase.ValidateResponse(results, nodesToWrite);

        return results;
    }

    /// <summary>
    /// Browse Server nodes
    /// </summary>
    public async Task<IEnumerable<ReferenceDescription>> BrowseAsync(CancellationToken ct = default)
    {
        await EnsureConnected(ct).ConfigureAwait(false);

        // Create a Browser object
        var browser = new Browser(Connection.Session)
        {
            // Set browse parameters
            BrowseDirection = BrowseDirection.Forward,
            NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable,
            ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
            IncludeSubtypes = true
        };

        var nodeToBrowse = ObjectIds.Server;

        // Call Browse service
        ReferenceDescriptionCollection browseResults =
            await browser.BrowseAsync(nodeToBrowse, ct).ConfigureAwait(false);

        return browseResults;
    }

    /// <summary>
    /// Call UA method
    /// </summary>
    public async Task<IList<object>> CallMethodAsync(NodeId target, NodeId method, CancellationToken ct = default, params object[] arguments)
    {
        await EnsureConnected(ct).ConfigureAwait(false);

        var outputArguments = await Connection.Session.CallAsync(
            target,
            method,
            ct,
            arguments).ConfigureAwait(false);
        
        return outputArguments;
    }

    /// <summary>
    /// Create Subscription and MonitoredItems for DataChanges
    /// </summary>
    public async Task<bool> SubscribeToDataChangesAsync(
        IEnumerable<ReadValueId> nodes,
        MonitoredItemNotificationEventHandler handler,
        uint minLifeTime,
        bool enableDurableSubscriptions,
        CancellationToken ct = default)
    {
        var isDurable = false;

        await EnsureConnected(ct).ConfigureAwait(false);

        // Create a subscription for receiving data change notifications
        const int subscriptionPublishingInterval = 1000;
        const int itemSamplingInterval = 1000;
        uint queueSize = 10;
        var lifetime = minLifeTime;

        if (enableDurableSubscriptions)
        {
            queueSize = 100;
            lifetime = 20;
        }

        // Define Subscription parameters
        var subscription = new Subscription(Connection.Session.DefaultSubscription)
        {
            DisplayName = $"Subscription-{Guid.NewGuid():N}",
            PublishingEnabled = true,
            PublishingInterval = subscriptionPublishingInterval,
            LifetimeCount = 0,
            MinLifetimeInterval = lifetime,
            KeepAliveCount = 5
        };

        Connection.Session.AddSubscription(subscription);

        // Create the subscription on Server side
        await subscription.CreateAsync(ct).ConfigureAwait(false);

        if (enableDurableSubscriptions)
        {
            var (success, revisedLifetimeInHours) =
                await subscription.SetSubscriptionDurableAsync(1, ct).ConfigureAwait(false);
            if (success)
            {
                isDurable = true;
            }
        }

        foreach (var node in nodes)
        {
            var monitoredItem = new MonitoredItem(subscription.DefaultItem)
            {
                // Int32 Node - Objects\CTT\Scalar\Simulation\Int32
                StartNodeId = node.NodeId,
                AttributeId = Attributes.Value,
                DisplayName = node.NodeId.ToString(),
                SamplingInterval = itemSamplingInterval,
                QueueSize = queueSize,
                DiscardOldest = true
            };
            monitoredItem.Notification += handler;
            
            subscription.AddItem(monitoredItem);
        }

        // Create the monitored items on Server side
        await subscription.ApplyChangesAsync(ct).ConfigureAwait(false);

        return isDurable;
    }
}