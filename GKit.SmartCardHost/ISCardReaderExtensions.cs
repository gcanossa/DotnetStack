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

    public static byte[] GetUid(this ISCardReader card)
    {
        var buffer = new byte[10];
        var error = card.Transmit([0xff, 0xca, 0, 0, 0], ref buffer); //TODO: verify result code
        error.ThrowIfNotSuccess();
        return buffer!;
    }

    //TODO: verify result code
    public static byte[] Read(this ISCardReader card, byte block, byte length)
    {
        var buffer = new byte[length + 2]; //2 bytes for status 9000 OK, 6300 KO
        var error = card.Transmit([0xff, 0xb0, 0, block, length], ref buffer);
        error.ThrowIfNotSuccess();
        return buffer!;
    }

    //TODO: verify result code
    public static void Write(this ISCardReader card, byte block, byte length, byte[] data)
    {
        byte[]? receiveBuffer = null;
        var error = card.Transmit([0xff, 0xd6, 0, block, length, .. data], ref receiveBuffer);
        error.ThrowIfNotSuccess();
    }
}