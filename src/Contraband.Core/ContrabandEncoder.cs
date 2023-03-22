using System.Collections;
using FFmpeg.NET;
using FFmpeg.NET.Enums;
using MessagePack;

namespace Contraband.Core;

public class ContrabandEncoder
{
    private readonly Engine _engine;

    public ContrabandEncoder(Engine engine)
    {
        _engine = engine;
    }

    private const int MaxFrameDimension = 4096;
    private const int MaxFrameArea = MaxFrameDimension * MaxFrameDimension;

    public async Task<Stream> GenerateVideo<T>(T data, CancellationToken cancellationToken = default)
    {
        var image = await GenerateFrame(data, cancellationToken);
        var imageId = Guid.NewGuid();
        var imagePath = $"{imageId}.png";
        var videoPath = $"{imageId}.mp4";
        await image.SaveAsPngAsync(imagePath, cancellationToken);

        try
        {
            var inputFile = new InputFile(imagePath);
            var options = new ConversionOptions
            {
                VideoFps = 30,
                VideoFormat = VideoFormat.mp4,
                VideoCodec = VideoCodec.libx264,
                CustomHeight = image.Height,
                CustomWidth = image.Width,
                VideoCodecPreset = VideoCodecPreset.veryslow,
                VideoCodecProfile = VideoCodecProfile.high,
                PixelFormat = "yuv420p",
                ExtraArguments = $"-loop 1 -movflags +faststart -crf 1 -tune stillimage -frames:v 4 -s {image.Width}x{image.Height}"
            };
            _engine.Error += (sender, args) => Console.WriteLine($"{sender} {args}");
            await _engine.ConvertAsync(inputFile, new OutputFile(videoPath), options, cancellationToken);
            var ms = new MemoryStream();
            await using var fileStream = File.OpenRead(videoPath);
            await fileStream.CopyToAsync(ms, cancellationToken);
            return ms;
        }
        finally
        {
            File.Delete(imagePath);
            if (File.Exists(videoPath))
                File.Delete(videoPath);
        }
    }

    internal async Task<Image> GenerateFrame<T>(T data, CancellationToken cancellationToken = default)
    {
        var serialized = MessagePackSerializer.Serialize(data, cancellationToken: cancellationToken);
        var header = BitConverter.GetBytes(serialized.Length);
        var payload = MergeUsingBlockCopy(header, serialized);
        if (payload.Length * 8 > MaxFrameArea)
            throw new ArgumentOutOfRangeException(nameof(data), "Value larger than permitted serializable size");
        var image = ConvertBytesToFrame(payload);
        return image;
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