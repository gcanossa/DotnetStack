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

    public event Action<ApiStatus>? StatusChanged;

    /// <summary>
    /// Routes a status onto the right slot. Replaces reflection over
    /// <c>typeof(T).Name.Replace("Client", "")</c>, which turned any rename into a runtime
    /// null-dereference instead of a compile error.
    /// </summary>
    internal void Set(RentriApi api, ApiStatus status)
    {
        switch (api)
        {
            case RentriApi.Anagrafiche: Anagrafiche = status; break;
            case RentriApi.CaRentri: CaRentri = status; break;
            case RentriApi.Codifiche: Codifiche = status; break;
            case RentriApi.DatiRegistri: DatiRegistri = status; break;
            case RentriApi.Formulari: Formulari = status; break;
            case RentriApi.VidimazioneFormulari: VidimazioneFormulari = status; break;
            default: throw new ArgumentOutOfRangeException(nameof(api), api, null);
        }
    }

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
            <= 400 => ApiStatus.Available,
            401 => ApiStatus.Unauthorized,
            403 => ApiStatus.Forbidden,
            423 => ApiStatus.Banned,
            429 => ApiStatus.RateLimited,
            _ => ApiStatus.Unavailable
        };
    }
}

public enum RentriApi
{
    Anagrafiche,
    CaRentri,
    Codifiche,
    DatiRegistri,
    Formulari,
    VidimazioneFormulari
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