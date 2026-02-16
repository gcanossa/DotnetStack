namespace GKit.RENTRI;

public abstract class BaseClientFactory<T> where T : BaseClient
{
    private readonly ApiStatusProvider apiStatusProvider;

    public BaseClientFactory(ApiStatusProvider apiStatusProvider)
    {
        this.apiStatusProvider = apiStatusProvider;
    }

    protected abstract T BuildClient();

    public T CreateClient()
    {
        var result = BuildClient();

        result.PrepareRequestHandler = (client, request, url) =>
        {

        };

        result.ProcessResponseHandler = (client, response) =>
        {
            ApiStatus status = ApiStatusProvider.GetApiStatusFromHttpStatusCode((int)response.StatusCode);

            var prop = typeof(ApiStatusProvider).GetProperty(typeof(T).Name.Replace("Client", string.Empty))!;

            prop.SetValue(apiStatusProvider, status);
        };

        return result;
    }
}