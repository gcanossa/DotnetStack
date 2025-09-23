using System.Runtime.CompilerServices;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;

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
    /// <summary>
    /// Loads the custom type system of the server in the session.
    /// </summary>
    /// <remarks>
    /// Outputs elapsed time information for perf testing and lists all
    /// types that were successfully added to the session encodeable type factory.
    /// </remarks>
    public async Task<ComplexTypeSystem> LoadTypeSystemAsync(CancellationToken ct = default)
    {
        await EnsureConnected(ct).ConfigureAwait(false);
        
        var complexTypeSystem = new ComplexTypeSystem(Connection.Session!);
        await complexTypeSystem.LoadAsync(ct: ct).ConfigureAwait(false);

        return complexTypeSystem;
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
        var response = await Connection.Session!.WriteAsync(
            null,
            nodesToWrite,
            ct).ConfigureAwait(false);

        var results = response.Results;

        // Validate the response
        ClientBase.ValidateResponse(results, nodesToWrite);

        return results;
    }
    
    /// <summary>
    /// Browse full address space using the ManagedBrowseMethod, which
    /// will take care of not sending to many nodes to the server,
    /// calling BrowseNext and dealing with the status codes
    /// BadNoContinuationPoint and BadInvalidContinuationPoint.
    /// </summary>
    /// <param name="uaClient">The UAClient with a session to use.</param>
    /// <param name="startingNode">The node where the browse operation starts.</param>
    /// <param name="browseDescription">An optional BrowseDescription to use.</param>
    public async Task<ReferenceDescriptionCollection> BrowseAsync(
        NodeId startingNode = null,
        BrowseDescription browseDescription = null,
        CancellationToken ct = default)
    {
        await EnsureConnected(ct).ConfigureAwait(false);
        
        var policyBackup = Connection.Session!.ContinuationPointPolicy;
        try
        {
            Connection.Session!.ContinuationPointPolicy = ContinuationPointPolicy.Default;

            var browseDirection = BrowseDirection.Forward;
            var referenceTypeId = ReferenceTypeIds.HierarchicalReferences;
            var includeSubtypes = true;
            uint nodeClassMask = 0;

            if (browseDescription != null)
            {
                startingNode = browseDescription.NodeId;
                browseDirection = browseDescription.BrowseDirection;
                referenceTypeId = browseDescription.ReferenceTypeId;
                includeSubtypes = browseDescription.IncludeSubtypes;
                nodeClassMask = browseDescription.NodeClassMask;
            }

            var nodesToBrowse = new List<NodeId> { startingNode ?? ObjectIds.RootFolder };

            const int kMaxReferencesPerNode = 1000;

            // Browse
            var referenceDescriptions = new Dictionary<ExpandedNodeId, ReferenceDescription>();

            var searchDepth = 0;
            var maxNodesPerBrowse = Connection.Session!.OperationLimits.MaxNodesPerBrowse;

            var allReferenceDescriptions = new List<ReferenceDescriptionCollection>();
            var newReferenceDescriptions = new List<ReferenceDescriptionCollection>();
            var allServiceResults = new List<ServiceResult>();

            while (nodesToBrowse.Count != 0 && searchDepth < 256)
            {
                searchDepth++;

                const bool repeatBrowse = false;

                do
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    // the resultMask defaults to "all"
                    // maybe the API should be extended to
                    // support it. But that will then also be
                    // necessary for BrowseAsync
                    (IList<ReferenceDescriptionCollection> descriptions, IList<ServiceResult> errors) =
                        await Connection
                            .Session!.ManagedBrowseAsync(
                                null,
                                null,
                                nodesToBrowse,
                                kMaxReferencesPerNode,
                                browseDirection,
                                referenceTypeId,
                                true,
                                nodeClassMask,
                                ct)
                            .ConfigureAwait(false);

                    allReferenceDescriptions.AddRange(descriptions);
                    newReferenceDescriptions.AddRange(descriptions);
                    allServiceResults.AddRange(errors);
                } while (repeatBrowse);

                // Build browse request for next level
                var nodesForNextManagedBrowse = new List<NodeId>();
                int duplicates = 0;
                foreach (var reference in
                         newReferenceDescriptions.SelectMany(referenceCollection => referenceCollection))
                {
                    if (referenceDescriptions.TryAdd(reference.NodeId, reference))
                    {
                        if (!reference.ReferenceTypeId.Equals(ReferenceTypeIds.HasProperty))
                        {
                            nodesForNextManagedBrowse.Add(
                                ExpandedNodeId.ToNodeId(
                                    reference.NodeId,
                                    Connection.Session!.NamespaceUris));
                        }
                    }
                    else
                    {
                        duplicates++;
                    }
                }

                newReferenceDescriptions.Clear();

                nodesToBrowse = nodesForNextManagedBrowse;
            }

            var result = new ReferenceDescriptionCollection(referenceDescriptions.Values);

            result.Sort((x, y) => x.NodeId.CompareTo(y.NodeId));

            return result;
        }
        finally
        {
            Connection.Session.ContinuationPointPolicy = policyBackup;
        }
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