using System.Net;
using System.Net.Http.Headers;
using GKit.RENTRI;
using GKit.Tests.Infrastructure;

namespace GKit.Tests.RENTRI;

/// <summary>
/// These live on <c>BaseClient</c>, which exists so the NSwag stubs can be regenerated without
/// losing the extensions layered on top of them — so fixing them needs no generated-code edits.
/// </summary>
public class BaseClientHeaderTests
{
  private static HttpResponseMessage WithHeader(string name, string value)
  {
    var response = new HttpResponseMessage(HttpStatusCode.OK);
    response.Headers.TryAddWithoutValidation(name, value);
    return response;
  }

  [Fact]
  public void Retry_After_in_seconds_is_read_as_seconds()
  {
    // TimeSpan.Parse("120") is 120 *days*. Any backoff honouring that never retries.
    using var response = WithHeader("Retry-After", "120");

    Assert.Equal(TimeSpan.FromSeconds(120), BaseClient.ParseRetryAfter(response));
  }

  [Fact]
  public void Retry_After_as_an_http_date_becomes_the_remaining_wait()
  {
    using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
    response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(2));

    var wait = BaseClient.ParseRetryAfter(response);

    Assert.NotNull(wait);
    Assert.InRange(wait.Value, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(130));
  }

  [Fact]
  public void An_http_date_already_in_the_past_means_retry_now()
  {
    using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
    response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(-5));

    Assert.Equal(TimeSpan.Zero, BaseClient.ParseRetryAfter(response));
  }

  [Fact]
  public void A_missing_Retry_After_is_null()
  {
    using var response = new HttpResponseMessage(HttpStatusCode.OK);

    Assert.Null(BaseClient.ParseRetryAfter(response));
  }

  [Theory]
  [InlineData("42", 42)]
  [InlineData("0", 0)]
  [InlineData("not-a-number", 0)]
  public void Paging_headers_are_parsed(string value, int expected)
  {
    using var response = WithHeader("Paging-PageSize", value);

    Assert.Equal(expected, BaseClient.ParseHeaderInt(response, "Paging-PageSize"));
  }

  [Fact]
  public void A_missing_paging_header_is_zero()
  {
    using var response = new HttpResponseMessage(HttpStatusCode.OK);

    Assert.Equal(0, BaseClient.ParseHeaderInt(response, "Paging-PageSize"));
  }

  [Fact]
  public void Paging_headers_do_not_depend_on_the_ambient_culture()
  {
    using var response = WithHeader("Paging-TotalRecordCount", "1234");

    using (new CultureScope("it-IT"))
      Assert.Equal(1234, BaseClient.ParseHeaderInt(response, "Paging-TotalRecordCount"));
  }
}
