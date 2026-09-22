using PCSC;

namespace GKit.SmartCardHost;

public static class ISCardReaderExtensions
{
    public static void ThrowIfNotSuccess(this SCardError error)
    {
        if (error != SCardError.Success)
        {
            throw new Exception($"SmartCard communication error: ${error}");
        }
    }

    /// <summary>
    /// Reads the card UID (APDU FF CA 00 00 00).
    /// <para>
    /// The response is the UID followed by a two-byte status word. Returning the whole buffer
    /// meant <c>Convert.ToHexString(GetUid())</c> produced a UID with the status word and any
    /// unused buffer bytes appended, and a 63 00 failure looked like success.
    /// </para>
    /// </summary>
    public static byte[] GetUid(this ISCardReader card)
    {
        var buffer = new byte[16];
        var error = card.Transmit([0xff, 0xca, 0, 0, 0], ref buffer);
        error.ThrowIfNotSuccess();

        return StripStatusWord(buffer);
    }

    /// <summary>Validates the trailing SW1 SW2 and returns the payload without it.</summary>
    internal static byte[] StripStatusWord(byte[] response)
    {
        if (response.Length < 2)
            throw new InvalidOperationException(
                $"SmartCard response too short to contain a status word ({response.Length} bytes)");

        var sw1 = response[^2];
        var sw2 = response[^1];

        if (sw1 != 0x90 || sw2 != 0x00)
            throw new InvalidOperationException(
                $"SmartCard returned status word {sw1:X2}{sw2:X2}");

        return response[..^2];
    }

    public static byte[] Read(this ISCardReader card, byte block, byte length)
    {
        var buffer = new byte[length + 2]; //2 bytes for status 9000 OK, 6300 KO
        var error = card.Transmit([0xff, 0xb0, 0, block, length], ref buffer);
        error.ThrowIfNotSuccess();

        return StripStatusWord(buffer);
    }

    //TODO: verify result code
    public static void Write(this ISCardReader card, byte block, byte length, byte[] data)
    {
        byte[]? receiveBuffer = null;
        var error = card.Transmit([0xff, 0xd6, 0, block, length, .. data], ref receiveBuffer);
        error.ThrowIfNotSuccess();
    }
}