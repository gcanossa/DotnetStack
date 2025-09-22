using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace GKit.OpcUa;

public class OpcUaConnection : IDisposable
{
    protected ILogger Logger => Utils.Logger;
    public ISession? Session { get; protected set; }
    protected SessionReconnectHandler SessionReconnectHandler { get; set; }
    protected IOpcUaContextOptions Options { get; }
    protected CertificateValidator? CertificateValidator { get; }
    protected ReverseConnectManager? ReverseConnectManager { get; }
    protected IUserIdentity? UserIdentity { get; }
    protected ApplicationConfiguration ApplicationConfiguration { get; }

    private bool _disposed;
    private bool _certificateValidatorRegistered;
    private readonly Lock m_lock = new();

    internal OpcUaConnection(IOpcUaContextOptions options)
    {
        Options = options;
        ReverseConnectManager = options.ReverseConnectManager?.Invoke();
        CertificateValidator = options.CertificateValidator?.Invoke();
        ApplicationConfiguration = options.ApplicationConfiguration;
        UserIdentity = options.UserIdentity?.Invoke();
    }

    public void Dispose()
    {
        _disposed = true;
        Utils.SilentDispose(Session);
        if (CertificateValidator is not null)
            CertificateValidator.CertificateValidation -= CertificateValidation;
        GC.SuppressFinalize(this);
    }

    public bool Connected => Session is { Connected: true };

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OpcUaConnection));

        if (CertificateValidator is not null && !_certificateValidatorRegistered)
        {
            CertificateValidator.CertificateValidation += CertificateValidation;
            _certificateValidatorRegistered = true;
        }

        try
        {
            if (Session is { Connected: true })
            {
                Logger.LogDebug("Session already connected!");
                return true;
            }

            ITransportWaitingConnection? connection = null;
            EndpointDescription? endpointDescription = null;
            if (ReverseConnectManager is not null)
            {
                Logger.LogInformation("Waiting for reverse connection to.... {ServerUrl}", Options.ServerUrl);

                do
                {
                    using var cts = new CancellationTokenSource(30_000);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        ct,
                        cts.Token);

                    connection = await ReverseConnectManager
                        .WaitForConnectionAsync(new Uri(Options.ServerUrl), null, linkedCts.Token);

                    if (connection == null)
                    {
                        throw new ServiceResultException(
                            StatusCodes.BadTimeout,
                            "Waiting for a reverse connection timed out."
                        );
                    }

                    if (endpointDescription != null) continue;

                    Logger.LogInformation("Discover reverse connection endpoints....");
                    endpointDescription = await CoreClientUtils.SelectEndpointAsync(
                        ApplicationConfiguration,
                        connection,
                        Options.UserIdentity is not null,
                        ct
                    ).ConfigureAwait(false);
                    connection = null;
                } while (connection == null);
            }
            else
            {
                Logger.LogInformation("Connecting to... {ServerUrl}", Options.ServerUrl);
                endpointDescription = await CoreClientUtils.SelectEndpointAsync(
                    ApplicationConfiguration,
                    Options.ServerUrl,
                    Options.UserIdentity is not null,
                    ct).ConfigureAwait(false);
            }

            // Get the endpoint by connecting to server's discovery endpoint.
            // Try to find the first endopint with security.
            var endpointConfiguration = EndpointConfiguration.Create(ApplicationConfiguration);
            var endpoint = new ConfiguredEndpoint(
                null,
                endpointDescription,
                endpointConfiguration);

            var sessionFactory = TraceableSessionFactory.Instance;

            // Create the session
            var session = await sessionFactory
                .CreateAsync(
                    ApplicationConfiguration,
                    connection,
                    endpoint,
                    connection == null,
                    false,
                    $"{ApplicationConfiguration.ApplicationName}-{Guid.NewGuid():N}",
                    Options.SessionLifeTime,
                    UserIdentity,
                    null,
                    ct
                )
                .ConfigureAwait(false);

            // Assign the created session
            if (session != null && session.Connected)
            {
                Session = session;

                // override keep alive interval
                Session.KeepAliveInterval = Options.KeepAliveInterval;

                // support transfer
                Session.DeleteSubscriptionsOnClose = false;
                Session.TransferSubscriptionsOnReconnect = true;

                // set up keep alive callback.
                Session.KeepAlive += Session_KeepAlive;

                // prepare a reconnect handler
                SessionReconnectHandler = new SessionReconnectHandler(
                    true,
                    Options.ReconnectPeriodExponentialBackoff);
            }

            // Session created successfully.
            Logger.LogInformation(
                "New Session Created with SessionName = {SessionName}",
                Session!.SessionName);

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Create Session Error : {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Disconnects the session.
    /// </summary>
    /// <param name="leaveChannelOpen">Leaves the channel open.</param>
    public async Task DisconnectAsync(bool leaveChannelOpen = false, CancellationToken ct = default)
    {
        try
        {
            if (Session != null)
            {
                Logger.LogInformation("Disconnecting...");

                lock (m_lock)
                {
                    Session.KeepAlive -= Session_KeepAlive;
                    SessionReconnectHandler?.Dispose();
                    SessionReconnectHandler = null;
                }

                await Session.CloseAsync(!leaveChannelOpen, ct).ConfigureAwait(false);
                if (leaveChannelOpen)
                {
                    // detach the channel, so it doesn't get
                    // closed when the session is disposed.
                    Session.DetachChannel();
                }

                Session.Dispose();
                Session = null;

                // Log Session Disconnected event
                Logger.LogInformation("Session Disconnected.");
            }
            else
            {
                Logger.LogWarning("Session not created!");
            }
        }
        catch (Exception ex)
        {
            // Log Error
            Logger.LogError("Disconnect Error : {Message}", ex.Message);
        }
    }

    protected virtual void CertificateValidation(
        CertificateValidator sender,
        CertificateValidationEventArgs e)
    {
        if (e.Error.StatusCode != StatusCodes.BadCertificateUntrusted) return;

        if (Options.AcceptUntrustedCertificates)
        {
            Logger.LogDebug("Untrusted Certificate accepted. Subject = {Subject}", e.Certificate.Subject);
            e.Accept = true;
        }
        else
        {
            Logger.LogDebug("Untrusted Certificate rejected. Subject = {Subject}", e.Certificate.Subject);
        }
    }

    /// <summary>
    /// Handles a keep alive event from a session and triggers a reconnect if necessary.
    /// </summary>
    private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        try
        {
            // check for events from discarded sessions.
            if (Session == null || !Session.Equals(session))
            {
                return;
            }

            // start reconnect sequence on communication error.
            if (ServiceResult.IsBad(e.Status) && ReverseConnectManager is not null)
            {
                if (Options.ReconnectPeriod <= 0)
                {
                    Logger.LogWarning(
                        "KeepAlive status {KeepAliveStatus}, but reconnect is disabled.",
                        e.Status);
                    return;
                }

                var state = SessionReconnectHandler
                    .BeginReconnect(
                        Session,
                        ReverseConnectManager!,
                        Options.ReconnectPeriod,
                        Client_ReconnectComplete!
                    );
                if (state == SessionReconnectHandler.ReconnectState.Triggered)
                {
                    Logger.LogInformation(
                        "KeepAlive status {KeepAliveStatus}, reconnect status {ReconnectStatus}, reconnect period {ReconnectPeriod}ms.",
                        e.Status,
                        state,
                        Options.ReconnectPeriod
                    );
                }
                else
                {
                    Logger.LogInformation(
                        "KeepAlive status {KeepAliveStatus}, reconnect status {ReconnectStatus}.",
                        e.Status,
                        state);
                }

                // cancel sending a new keep alive request, because reconnect is triggered.
                e.CancelKeepAlive = true;
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error in OnKeepAlive.");
        }
    }

    /// <summary>
    /// Called when the reconnect attempt was successful.
    /// </summary>
    private void Client_ReconnectComplete(object sender, EventArgs e)
    {
        // ignore callbacks from discarded objects.
        if (!ReferenceEquals(sender, SessionReconnectHandler))
        {
            return;
        }

        lock (m_lock)
        {
            // if session recovered, Session property is null
            if (SessionReconnectHandler.Session != null)
            {
                // ensure only a new instance is disposed
                // after reactivate, the same session instance may be returned
                if (!ReferenceEquals(Session, SessionReconnectHandler.Session))
                {
                    Logger.LogInformation(
                        "--- RECONNECTED TO NEW SESSION --- {SessionId}",
                        SessionReconnectHandler.Session.SessionId
                    );
                    var session = Session;
                    Session = SessionReconnectHandler.Session;
                    Utils.SilentDispose(session);
                }
                else
                {
                    Logger.LogInformation(
                        "--- REACTIVATED SESSION --- {SessionId}",
                        SessionReconnectHandler.Session.SessionId);
                }
            }
            else
            {
                Logger.LogInformation("--- RECONNECT KeepAlive recovered ---");
            }
        }
    }
}