using System.ComponentModel;
using System.Globalization;

namespace GKit.Settings;

/// <summary>
/// Converts setting values to and from their stored string form.
/// <para>
/// Always invariant: persisted settings travel between machines (and between a machine and its
/// own backup), so a <c>decimal</c> written as "1,5" on an it-IT host must not read back as 15
/// on an en-US one. <see cref="Convert.ChangeType(object?, Type)"/> is deliberately avoided —
/// it is culture-sensitive and cannot target <see cref="Nullable{T}"/>, enums, <see cref="Guid"/>
/// or <see cref="Uri"/>.
/// </para>
/// </summary>
public static class SettingValueConverter
{
  public static object? FromStorage(string? value, Type targetType)
  {
    ArgumentNullException.ThrowIfNull(targetType);

    var underlying = Nullable.GetUnderlyingType(targetType);

    if (string.IsNullOrEmpty(value))
      return underlying is not null || !targetType.IsValueType ? null : Activator.CreateInstance(targetType);

    var effectiveType = underlying ?? targetType;

    if (effectiveType == typeof(string)) return value;
    if (effectiveType.IsEnum) return Enum.Parse(effectiveType, value, ignoreCase: true);

    var converter = TypeDescriptor.GetConverter(effectiveType);
    if (!converter.CanConvertFrom(typeof(string)))
      throw new NotSupportedException(
        $"No invariant string conversion is available for setting type '{effectiveType.FullName}'.");

    return converter.ConvertFromInvariantString(value);
  }

  public static string ToStorage(object? value) => value switch
  {
    null => "",
    string s => s,
    Enum e => e.ToString(),
    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
    _ => TypeDescriptor.GetConverter(value.GetType()).ConvertToInvariantString(value) ?? ""
  };
}
