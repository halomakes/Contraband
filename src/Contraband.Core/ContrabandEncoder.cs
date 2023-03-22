using System.Collections;
using MessagePack;

namespace Contraband.Core;

public class ContrabandEncoder
{
    private const int MaxFrameDimension = 4096;
    private const int MaxFrameArea = MaxFrameDimension * MaxFrameDimension;

    internal async Task<Stream> GenerateFrame<T>(T data, CancellationToken cancellationToken = default)
    {
        var serialized = MessagePackSerializer.Serialize(data, cancellationToken: cancellationToken);
        var header = BitConverter.GetBytes(serialized.Length);
        var payload = MergeUsingBlockCopy(header, serialized);
        if (payload.Length * 8 > MaxFrameArea)
            throw new ArgumentOutOfRangeException(nameof(data), "Value larger than permitted serializable size");
        using var image = ConvertBytesToFrame(payload);
        var memoryStream = new MemoryStream();
        await image.SaveAsPngAsync("test.png", cancellationToken);
        await image.SaveAsPngAsync(memoryStream, cancellationToken);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }

    internal Image<Rgb24> ConvertBytesToFrame(byte[] payload)
    {
        var bits = new BitArray(payload);
        var frameDimension = CalculateDimension(bits.Length);
        var image = new Image<Rgb24>(frameDimension, frameDimension);
        var bitIndex = 0;
        var white = Color.White;
        var black = Color.Black;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < frameDimension; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (var x = 0; x < frameDimension; x++)
                {
                    pixelRow[x] = bits[bitIndex++] ? white : black;

                    if (bitIndex == bits.Length)
                        return;
                }
            }
        });
        return image;

        static int CalculateDimension(int payloadSize)
        {
            var squareRoot = Math.Sqrt(payloadSize);
            return (int)Math.Ceiling(squareRoot);
        }
    }

    private static byte[] MergeUsingBlockCopy(byte[] firstArray, byte[] secondArray)
    {
        var combinedArray = new byte[firstArray.Length + secondArray.Length];
        Buffer.BlockCopy(firstArray, 0, combinedArray, 0, firstArray.Length);
        Buffer.BlockCopy(secondArray, 0, combinedArray, firstArray.Length, secondArray.Length);
        return combinedArray;
    }
}