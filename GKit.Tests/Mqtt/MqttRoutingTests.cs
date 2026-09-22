using GKit.Mqtt;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Diagnostics.Logger;

namespace GKit.Tests.Mqtt;

/// <summary>
/// Topic dispatch and log formatting are pure logic that happened to sit behind a broker.
/// Neither needs an MQTT server to verify.
/// </summary>
public class MqttTopicRouterTests
{
  private sealed class SensorController : MqttControllerBase
  {
    [MqttTopic("sensors/+/temp")]
    public Task OnTemperature(MqttApplicationMessageReceivedEventArgs args) => Task.CompletedTask;

    [MqttTopic("alerts/#")]
    public Task OnAlert(MqttApplicationMessageReceivedEventArgs args) => Task.CompletedTask;

    [MqttTopic("exact/topic")]
    public Task OnExact(MqttApplicationMessageReceivedEventArgs args) => Task.CompletedTask;

    [MqttTopic("exact/topic")]
    public Task AlsoOnExact(MqttApplicationMessageReceivedEventArgs args) => Task.CompletedTask;

    public Task NotARoute(MqttApplicationMessageReceivedEventArgs args) => Task.CompletedTask;
  }

  private sealed class WrongReturnType : MqttControllerBase
  {
    [MqttTopic("a")] public void Handle(MqttApplicationMessageReceivedEventArgs args) { }
  }

  private sealed class WrongParameter : MqttControllerBase
  {
    [MqttTopic("a")] public Task Handle(string topic) => Task.CompletedTask;
  }

  private static readonly MqttTopicRouter Router = new(typeof(SensorController));

  [Fact]
  public void A_single_level_wildcard_matches_a_concrete_topic()
  {
    // Dispatch was an exact dictionary lookup, so a handler for "sensors/+/temp" subscribed
    // successfully and then never fired for "sensors/3/temp".
    var handlers = Router.Resolve("sensors/3/temp");

    Assert.Single(handlers);
    Assert.Equal(nameof(SensorController.OnTemperature), handlers[0].Name);
  }

  [Fact]
  public void A_multi_level_wildcard_matches_any_depth()
  {
    Assert.Single(Router.Resolve("alerts/critical"));
    Assert.Single(Router.Resolve("alerts/critical/disk/full"));
  }

  [Fact]
  public void A_single_level_wildcard_does_not_cross_a_separator()
  {
    Assert.Empty(Router.Resolve("sensors/3/inner/temp"));
  }

  [Fact]
  public void An_exact_topic_still_matches()
  {
    Assert.Equal(2, Router.Resolve("exact/topic").Count);
  }

  [Fact]
  public void An_unrelated_topic_matches_nothing()
  {
    Assert.Empty(Router.Resolve("something/else"));
  }

  [Fact]
  public void Only_attributed_methods_become_routes()
  {
    Assert.DoesNotContain(Router.TopicFilters, t => t == nameof(SensorController.NotARoute));
    Assert.Equal(3, Router.TopicFilters.Count);
  }

  [Fact]
  public void A_handler_that_does_not_return_Task_is_rejected_at_construction()
  {
    var ex = Assert.Throws<ArgumentException>(() => new MqttTopicRouter(typeof(WrongReturnType)));

    Assert.Contains("Task", ex.Message);
  }

  [Fact]
  public void A_handler_with_the_wrong_first_parameter_is_rejected_at_construction()
  {
    var ex = Assert.Throws<ArgumentException>(() => new MqttTopicRouter(typeof(WrongParameter)));

    Assert.Contains(nameof(MqttApplicationMessageReceivedEventArgs), ex.Message);
  }
}

public class MqttLoggerTests
{
  [Fact]
  public void The_message_template_is_formatted_with_MQTTnets_own_parameters()
  {
    // MQTTnet's parameters used to be bound to {Source}/{Message} instead, so the structured
    // log carried the wrong values and source/message were never recorded.
    var formatted = MqttLogger.Format("Connected to {0}:{1}", ["broker", 1883]);

    Assert.Equal("Connected to broker:1883", formatted);
  }

  [Fact]
  public void A_template_with_no_parameters_is_passed_through()
  {
    Assert.Equal("plain", MqttLogger.Format("plain", null));
    Assert.Equal("plain", MqttLogger.Format("plain", []));
  }

  [Fact]
  public void A_malformed_template_does_not_take_down_the_logging_path()
  {
    // A dependency's bad template must degrade, not throw inside the log pipeline.
    var formatted = MqttLogger.Format("unbalanced {0} {1}", ["only-one"]);

    Assert.Equal("unbalanced {0} {1}", formatted);
  }

  [Theory]
  [InlineData(MqttNetLogLevel.Info, LogLevel.Information)]
  [InlineData(MqttNetLogLevel.Warning, LogLevel.Warning)]
  [InlineData(MqttNetLogLevel.Error, LogLevel.Error)]
  [InlineData(MqttNetLogLevel.Verbose, LogLevel.Debug)]
  public void MQTTnet_levels_map_onto_logging_levels(MqttNetLogLevel source, LogLevel expected)
  {
    Assert.Equal(expected, MqttLogger.ToLogLevel(source));
  }
}
