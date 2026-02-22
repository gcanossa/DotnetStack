namespace GKit.RENTRI;

public static class ClientOptionsExtensions
{
    public static ClientOptions ToDemo(this ClientOptions clientOptions)
    {
        return new ClientOptions()
        {
            Certificate = clientOptions.Certificate,
            Issuer = clientOptions.Issuer,
            BaseUrl = "https://demoapi.rentri.gov.it",
            Audience = "rentrigov.demo.api",
        };
    }

    public static ClientOptions AsDemo(this ClientOptions clientOptions)
    {
        clientOptions.Audience = "rentrigov.demo.api";
        clientOptions.BaseUrl = "https://demoapi.rentri.gov.it";
        return clientOptions;
    }

    public static ClientOptions ToLive(this ClientOptions clientOptions)
    {
        return new ClientOptions()
        {
            Certificate = clientOptions.Certificate,
            Issuer = clientOptions.Issuer,
            BaseUrl = "https://api.rentri.gov.it",
            Audience = "rentrigov.api",
        };
    }

    public static ClientOptions AsLive(this ClientOptions clientOptions)
    {
        clientOptions.Audience = "rentrigov.api";
        clientOptions.BaseUrl = "https://api.rentri.gov.it";
        return clientOptions;
    }

    public static bool IsDemo(this ClientOptions clientOptions)
    {
        return clientOptions.Audience == "rentrigov.demo.api" && clientOptions.BaseUrl == "https://demoapi.rentri.gov.it";
    }

    public static bool IsLive(this ClientOptions clientOptions)
    {
        return clientOptions.Audience == "rentrigov.api" && clientOptions.BaseUrl == "https://api.rentri.gov.it";
    }
}