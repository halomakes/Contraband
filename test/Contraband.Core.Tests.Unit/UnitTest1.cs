using System.Collections;
using Bogus;
using FFmpeg.NET;
using FluentAssertions;
using MessagePack;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Sdk;

namespace Contraband.Core.Tests.Unit;

public class UnitTest1
{
    [Fact]
    public async Task Generate_CreatesImage_WhenInputIsObject()
    {
        var objectToEncode = Generator.Generate();
        await using var image = await GetStream(objectToEncode);
        image.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task Generate_CreatesImage_WhenInputIsCollection()
    {
        var objectToEncode = Generator.GenerateBetween(2, 5);
        await using var image = await GetStream(objectToEncode);
        image.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task Header_ContainsDataLength()
    {
        var objectToEncode = Generator.GenerateBetween(1, 50);
        var dataLength = MessagePackSerializer.Serialize(objectToEncode).Length;
        await using var content = await GetStream(objectToEncode);
        var bytes = await GetBytes(content);
        var header = bytes[..4];
        var headerValue = BitConverter.ToInt32(header);
        headerValue.Should().Be(dataLength);
    }

    [Fact]
    public async Task Encode_ReturnsVideoStream()
    {
        var objectToEncode = Generator.GenerateBetween(1, 50);
        var engine = new Engine("C:\\ProgramData\\chocolatey\\bin\\ffmpeg.exe");
        var encoder = new ContrabandEncoder(engine);
        var stream = await encoder.GenerateVideo(objectToEncode);
        stream.CanRead.Should().BeTrue();
    } 

    protected static async Task<Stream> GetStream<T>(T content)
    {
        var image = await new ContrabandEncoder(null!).GenerateFrame(content);
        var memoryStream = new MemoryStream();
        await image.SaveAsPngAsync(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }

    protected static async Task<byte[]> GetBytes(Stream sourceStream)
    {
        var bits = await GetBits(sourceStream).ToListAsync();
        return bits.Chunk(8).Where(c => c.Length == 8).Select(chunk => ToByte(new BitArray(chunk))).ToArray();
    }

    protected static async IAsyncEnumerable<bool> GetBits(Stream sourceStream)
    {
        var image = await Image.LoadAsync(sourceStream);
        if (image is not Image<Rgb24> parsedImage)
            throw new XunitException("Image not recognized");

        foreach (var group in parsedImage.GetPixelMemoryGroup().ToArray())
        foreach (var pixel in group.ToArray())
            yield return pixel.R > byte.MaxValue / 2;
    }

    protected static readonly Faker<MySerializableType> Generator = new Faker<MySerializableType>()
        .RuleFor(t => t.Potato, faker => faker.Lorem.Sentence())
        .RuleFor(t => t.Yeet, faker => faker.Random.Bool())
        .RuleFor(t => t.Okay, faker => faker.Random.Double());

    public static byte ToByte(BitArray bits)
    {
        if (bits.Count != 8)
        {
            throw new ArgumentException(nameof(bits));
        }

        byte[] bytes = new byte[1];
        bits.CopyTo(bytes, 0);
        return bytes[0];
    }

    [MessagePackObject]
    public class MySerializableType
    {
        [Key(0)] public required string Potato { get; set; }

        [Key(1)] public bool Yeet { get; set; }

        [Key(2)] public double Okay { get; set; }
    }
}