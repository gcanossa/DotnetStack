using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace GKit.Application;

public static class ApplicationExtensions
{

  public static IEndpointConventionBuilder MapHealthChecksJson(this WebApplication ext, string pattern = "/api/health")
  {
    var options = new HealthCheckOptions
    {
      ResponseWriter = (context, healthReport) =>
      {
        context.Response.ContentType = "application/json; charset=utf-8";

        var jsonOptions = new JsonWriterOptions { Indented = true };

        using var memoryStream = new MemoryStream();
        using (var jsonWriter = new Utf8JsonWriter(memoryStream, jsonOptions))
        {
          jsonWriter.WriteStartObject();
          jsonWriter.WriteString("status", healthReport.Status.ToString());
          jsonWriter.WriteStartObject("results");

          foreach (var healthReportEntry in healthReport.Entries)
          {
            jsonWriter.WriteStartObject(healthReportEntry.Key);
            jsonWriter.WriteString("status",
              healthReportEntry.Value.Status.ToString());
            jsonWriter.WriteString("description",
              healthReportEntry.Value.Description);
            jsonWriter.WriteStartObject("data");

            foreach (var item in healthReportEntry.Value.Data)
            {
              jsonWriter.WritePropertyName(item.Key);

              JsonSerializer.Serialize(jsonWriter, item.Value,
                item.Value?.GetType() ?? typeof(object));
            }

            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();
          }

          jsonWriter.WriteEndObject();
          jsonWriter.WriteEndObject();
        }

        return context.Response.WriteAsync(
          Encoding.UTF8.GetString(memoryStream.ToArray()));
      }
    };

    return ext.MapHealthChecks(pattern, options);
  }

  public static IApplicationBuilder ApplyPendingMigrations<T>(this WebApplication ext)
    where T : DbContext
  {
    if (!ext.Environment.IsDevelopment())
    {
      using var scope = ext.Services.CreateScope();
      using var ctx = scope.ServiceProvider.GetRequiredService<T>();

      var migrations = ctx.Database.GetPendingMigrations();
      if (migrations.Any())
      {
        Log.Information("Found {Count} not applied migrations. Applying...", migrations.Count());

        ctx.Database.Migrate();

        Log.Information("Migrations applied");
      }
    }
    return ext;
  }
}
