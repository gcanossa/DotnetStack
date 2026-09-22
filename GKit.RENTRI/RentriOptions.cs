namespace GKit.RENTRI;

public enum RentriEnvironment
{
    Live,
    Demo
}

public static class RentriEndpoints
{
    public const string LiveBaseUrl = "https://api.rentri.gov.it";
    public const string DemoBaseUrl = "https://demoapi.rentri.gov.it";

    public const string LiveAudience = "rentrigov.api";
    public const string DemoAudience = "rentrigov.demo.api";

    public static string BaseUrlFor(RentriEnvironment environment) =>
        environment == RentriEnvironment.Demo ? DemoBaseUrl : LiveBaseUrl;

    public static string AudienceFor(RentriEnvironment environment) =>
        environment == RentriEnvironment.Demo ? DemoAudience : LiveAudience;

    /// <summary>
    /// Repoints a generated stub URL at <paramref name="baseUrl"/>, keeping its path.
    /// A plain string replacement of the hard-coded live host silently left the client pointing
    /// at production whenever the generated URL did not match character for character.
    /// </summary>
    public static string Rebase(string generatedUrl, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return generatedUrl;

        var target = baseUrl.TrimEnd('/');

        if (!Uri.TryCreate(generatedUrl, UriKind.Absolute, out var generated))
            return target;

        return $"{target}{generated.PathAndQuery.TrimEnd('/')}";
    }
}

public class RentriOptions
{
    /// <summary>
    /// Which RENTRI environment unauthenticated calls (the status probes) should target.
    /// Must match the environment of the <see cref="ClientOptions"/> used for real calls,
    /// otherwise the health check reports on an environment the application never talks to.
    /// </summary>
    public RentriEnvironment Environment { get; set; } = RentriEnvironment.Live;

    /// <summary>How often <see cref="ApiStatusService"/> polls. Set to <c>null</c> to disable.</summary>
    public TimeSpan? StatusPollInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Timeout applied to each status probe.</summary>
    public TimeSpan StatusProbeTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
