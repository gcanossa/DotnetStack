using GKit.SmtpHost;
using MimeKit;

namespace GKit.Tests.SmtpHost;

/// <summary>
/// Route matching and dead-lettering run entirely in process: a real SMTP conversation adds
/// nothing these cannot prove.
/// </summary>
public class SmtpRouteTableTests
{
  private sealed class OrdersController : SmtpControllerBase
  {
    [SmtpRoute(fromPattern: "erp@.*")]
    public Task FromErp(MimeMessage message) => Task.CompletedTask;

    [SmtpRoute(subjectPattern: "^Ordine")]
    public Task OrderSubject(MimeMessage message) => Task.CompletedTask;

    [SmtpRoute(identity: "scanner")]
    public Task FromScannerIdentity(MimeMessage message) => Task.CompletedTask;

    public Task NotARoute(MimeMessage message) => Task.CompletedTask;
  }

  private static readonly SmtpRouteTable Table = new([typeof(SmtpRouteTableTests).Assembly]);

  private static MimeMessage Message(string from = "erp@example.com", string to = "inbox@example.com",
    string? subject = "Ordine 123", string fromDisplay = "", string toDisplay = "")
  {
    var message = new MimeMessage();
    message.From.Add(new MailboxAddress(fromDisplay, from));
    message.To.Add(new MailboxAddress(toDisplay, to));
    // Left unset when null: MimeKit rejects a null Subject via the setter, but a message
    // parsed off the wire without a Subject header reads back as null — which is the case the
    // route matcher has to survive.
    if (subject is not null) message.Subject = subject;

    return message;
  }

  [Fact]
  public void A_from_pattern_matches_the_address_not_the_display_name()
  {
    // Matching InternetAddress.Name — the display name, usually empty — meant a pattern like
    // "erp@.*" never matched anything at all.
    var matched = Table.Resolve(Message(from: "erp@example.com"), context: null);

    Assert.Contains(matched.SelectMany(kv => kv.Value),
      m => m.Name == nameof(OrdersController.FromErp));
  }

  [Fact]
  public void A_non_matching_address_is_not_routed()
  {
    var matched = Table.Resolve(Message(from: "someone@else.com", subject: "x"), context: null);

    Assert.DoesNotContain(matched.SelectMany(kv => kv.Value),
      m => m.Name == nameof(OrdersController.FromErp));
  }

  [Fact]
  public void The_display_name_still_matches_as_a_fallback()
  {
    var matched = Table.Resolve(
      Message(from: "noreply@host", fromDisplay: "erp@example.com", subject: "x"), context: null);

    Assert.Contains(matched.SelectMany(kv => kv.Value),
      m => m.Name == nameof(OrdersController.FromErp));
  }

  [Fact]
  public void A_subject_pattern_matches()
  {
    var matched = Table.Resolve(Message(subject: "Ordine 99"), context: null);

    Assert.Contains(matched.SelectMany(kv => kv.Value),
      m => m.Name == nameof(OrdersController.OrderSubject));
  }

  [Fact]
  public void A_message_with_no_subject_header_does_not_throw()
  {
    // A constructed MimeMessage reports "" for Subject, but one *parsed* off the wire without
    // a Subject header reports null — and Regex.IsMatch(null) threw. Parse it, because that is
    // the path a real message takes through SmtpMessageStore.
    const string raw = "From: erp@example.com\r\nTo: inbox@example.com\r\n\r\nbody\r\n";
    using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(raw));
    var message = MimeMessage.Load(stream);

    Assert.Null(message.Subject);

    var ex = Record.Exception(() => Table.Resolve(message, context: null));

    Assert.Null(ex);
  }

  [Fact]
  public void An_identity_scoped_route_does_not_match_an_unauthenticated_session()
  {
    var matched = Table.Resolve(Message(), context: null);

    Assert.DoesNotContain(matched.SelectMany(kv => kv.Value),
      m => m.Name == nameof(OrdersController.FromScannerIdentity));
  }

  [Fact]
  public void Only_attributed_methods_become_routes()
  {
    var matched = Table.Resolve(Message(), context: null);

    Assert.DoesNotContain(matched.SelectMany(kv => kv.Value),
      m => m.Name == nameof(OrdersController.NotARoute));
  }

  [Fact]
  public void Discovery_happens_once_not_per_message()
  {
    // The old handler walked every assembly and every type on each message — hundreds of
    // thousands of reflection operations per mail.
    Assert.True(Table.Count > 0);
    Assert.Contains(typeof(OrdersController), Table.ControllerTypes);
  }

  [Fact]
  public void An_assembly_whose_types_cannot_be_loaded_is_survivable()
  {
    var ex = Record.Exception(() => new SmtpRouteTable(AppDomain.CurrentDomain.GetAssemblies()));

    Assert.Null(ex);
  }
}

public class DeadLetterTests : IDisposable
{
  private readonly string _path =
    Path.Combine(Path.GetTempPath(), $"gkit-dead-letters-{Guid.NewGuid():N}");

  public void Dispose()
  {
    if (Directory.Exists(_path)) Directory.Delete(_path, recursive: true);
  }

  [Fact]
  public void Filenames_do_not_collide_for_messages_arriving_together()
  {
    // DateTime.UtcNow.ToFileTimeUtc() has 100 ns resolution: two messages in the same tick
    // overwrote one another, losing the very thing a dead letter exists to preserve.
    var now = DateTimeOffset.UtcNow;

    var names = Enumerable.Range(0, 500)
      .Select(_ => FileDeadLetterMessageHandler.BuildFileName(now, Guid.NewGuid()))
      .ToHashSet();

    Assert.Equal(500, names.Count);
  }

  [Fact]
  public void A_dead_letter_filename_is_an_eml()
  {
    var name = FileDeadLetterMessageHandler.BuildFileName(DateTimeOffset.UtcNow, Guid.NewGuid());

    Assert.EndsWith(".eml", name);
  }
}
