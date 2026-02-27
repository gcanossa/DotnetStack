namespace GKit.RENTRI;

public class ApiStatusProvider
{
    public ApiStatus Anagrafiche
    {
        get;
        internal set
        {
            field = value;
            StatusChanged?.Invoke(value);
        }
    } = ApiStatus.Unavailable;

    public ApiStatus CaRentri
    {
        get;
        internal set
        {
            field = value;
            StatusChanged?.Invoke(value);
        }
    } = ApiStatus.Unavailable;

    public ApiStatus Codifiche
    {
        get;
        internal set
        {
            field = value;
            StatusChanged?.Invoke(value);
        }
    } = ApiStatus.Unavailable;

    public ApiStatus DatiRegistri
    {
        get;
        internal set
        {
            field = value;
            StatusChanged?.Invoke(value);
        }
    } = ApiStatus.Unavailable;

    public ApiStatus Formulari
    {
        get;
        internal set
        {
            field = value;
            StatusChanged?.Invoke(value);
        }
    } = ApiStatus.Unavailable;

    public ApiStatus VidimazioneFormulari
    {
        get;
        internal set
        {
            field = value;
            StatusChanged?.Invoke(value);
        }
    } = ApiStatus.Unavailable;

    public event Action<ApiStatus> StatusChanged;

    public ApiStatus Status
    {
        get
        {
            var statuses = new[]
            {
                this.Anagrafiche, this.CaRentri, this.Codifiche, this.DatiRegistri, this.Formulari,
                this.VidimazioneFormulari
            };

            return statuses.All(p => p == ApiStatus.Available) ? ApiStatus.Available :
                statuses.Any(p => p == ApiStatus.Unauthorized) ? ApiStatus.Unauthorized :
                statuses.Any(p => p == ApiStatus.Forbidden) ? ApiStatus.Forbidden :
                statuses.Any(p => p == ApiStatus.Banned) ? ApiStatus.Banned :
                statuses.Any(p => p == ApiStatus.RateLimited) ? ApiStatus.RateLimited :
                ApiStatus.Unavailable;
        }
    }

    internal static ApiStatus GetApiStatusFromHttpStatusCode(int statusCode)
    {
        return statusCode switch
        {
            < 400 => ApiStatus.Available,
            401 => ApiStatus.Unauthorized,
            403 => ApiStatus.Forbidden,
            423 => ApiStatus.Banned,
            429 => ApiStatus.RateLimited,
            _ => ApiStatus.Unavailable
        };
    }
}

public enum ApiStatus
{
    Unauthorized,
    Forbidden,
    Available,
    Unavailable,
    Banned,
    RateLimited
}