using System.Collections;
using FFmpeg.NET;
using FFmpeg.NET.Enums;

namespace Contraband.Core;

public class ContrabandEncoder
{
    private readonly Engine _engine;

    public ContrabandEncoder(Engine engine)
    {
        _engine = engine;
    }

    private const int MinFrameDimension = 64;
    private const int MaxFrameDimension = 4096;
    private const int MaxFrameArea = MaxFrameDimension * MaxFrameDimension;

    public async Task<Stream> GenerateVideo(byte[] data, CancellationToken cancellationToken = default)
    {
        var image = GenerateFrame(data);
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
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
        finally
        {
            File.Delete(imagePath);
            if (File.Exists(videoPath))
                File.Delete(videoPath);
        }
    }

    internal Image GenerateFrame(byte[] data)
    {
        var sizeHeader = BitConverter.GetBytes(data.Length);
        var hashHeader = BitConverter.GetBytes(GetHashFromBytes(data));
        var payload = MergeUsingBlockCopy(sizeHeader, hashHeader, data);
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
            var neededSize = (int)Math.Ceiling(squareRoot);
            return neededSize < MinFrameDimension ? MinFrameDimension : neededSize;
        }
    }

    private static byte[] MergeUsingBlockCopy(params byte[][] arrays)
    {
        var combinedArray = new byte[arrays.Sum(a => a.Length)];
        var currentIndex = 0;
        foreach (var array in arrays)
        {
            Buffer.BlockCopy(array, 0, combinedArray, currentIndex, array.Length);
            currentIndex += array.Length;
        }

        return combinedArray;
    }
    
    internal static int GetHashFromBytes(byte[] bytes)
    {
        var hashCode = new HashCode();
        hashCode.AddBytes(bytes);
        return hashCode.ToHashCode();
    }
}