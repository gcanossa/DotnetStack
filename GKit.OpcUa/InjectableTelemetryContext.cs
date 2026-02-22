using Microsoft.Extensions.Logging;
using Opc.Ua;

namespace GKit.OpcUa;

public class InjectableTelemetryContext(ILoggerFactory loggerFactory) : TelemetryContextBase(loggerFactory);