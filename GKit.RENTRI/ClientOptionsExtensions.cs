namespace GKit.RENTRI;

public static class ClientOptionsExtensions
{
    public static ClientOptions ToDemo(this ClientOptions clientOptions) =>
        clientOptions.To(RentriEnvironment.Demo);

    public static ClientOptions AsDemo(this ClientOptions clientOptions) =>
        clientOptions.As(RentriEnvironment.Demo);

    public static ClientOptions ToLive(this ClientOptions clientOptions) =>
        clientOptions.To(RentriEnvironment.Live);

    public static ClientOptions AsLive(this ClientOptions clientOptions) =>
        clientOptions.As(RentriEnvironment.Live);

    /// <summary>Returns a copy of <paramref name="clientOptions"/> pointed at <paramref name="environment"/>.</summary>
    public static ClientOptions To(this ClientOptions clientOptions, RentriEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(clientOptions);

        return new ClientOptions()
        {
            Certificate = clientOptions.Certificate,
            Issuer = clientOptions.Issuer,
            BaseUrl = RentriEndpoints.BaseUrlFor(environment),
            Audience = RentriEndpoints.AudienceFor(environment),
        };
    }

    /// <summary>Mutates <paramref name="clientOptions"/> to point at <paramref name="environment"/>.</summary>
    public static ClientOptions As(this ClientOptions clientOptions, RentriEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(clientOptions);

        clientOptions.Audience = RentriEndpoints.AudienceFor(environment);
        clientOptions.BaseUrl = RentriEndpoints.BaseUrlFor(environment);
        return clientOptions;
    }

    public static bool IsDemo(this ClientOptions clientOptions) => clientOptions.Is(RentriEnvironment.Demo);

    public static bool IsLive(this ClientOptions clientOptions) => clientOptions.Is(RentriEnvironment.Live);

    public static bool Is(this ClientOptions clientOptions, RentriEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(clientOptions);

        return clientOptions.Audience == RentriEndpoints.AudienceFor(environment)
            && clientOptions.BaseUrl == RentriEndpoints.BaseUrlFor(environment);
    }

    public static RentriEnvironment? Environment(this ClientOptions clientOptions) =>
        clientOptions.Is(RentriEnvironment.Demo) ? RentriEnvironment.Demo
        : clientOptions.Is(RentriEnvironment.Live) ? RentriEnvironment.Live
        : null;
}
