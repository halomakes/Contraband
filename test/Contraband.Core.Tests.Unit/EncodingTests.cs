using System.Collections;
using FFmpeg.NET;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Sdk;

namespace Contraband.Core.Tests.Unit;

public class EncodingTests
{
    [Fact]
    public async Task Generate_CreatesImage_WhenInputIsObject()
    {
        var objectToEncode = GetRandomBytes();
        await using var image = await GetStream(objectToEncode);
        image.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task Header_ContainsDataLength()
    {
        var objectToEncode = GetRandomBytes();
        var dataLength = objectToEncode.Length;
        await using var content = await GetStream(objectToEncode);
        var bytes = await GetBytes(content);
        var header = bytes[..4];
        var headerValue = BitConverter.ToInt32(header);
        headerValue.Should().Be(dataLength);
    }

    [Fact]
    public async Task Header_ContainsDataHashLength()
    {
        var objectToEncode = GetRandomBytes();
        var dataHash = ContrabandEncoder.GetHashFromBytes(objectToEncode);
        await using var content = await GetStream(objectToEncode);
        var bytes = await GetBytes(content);
        var header = bytes[4..8];
        var headerValue = BitConverter.ToInt32(header);
        headerValue.Should().Be(dataHash);
    }
    
    [Fact]
    public async Task HeaderLength_MatchesContentLength()
    {
        var objectToEncode = GetRandomBytes();
        await using var content = await GetStream(objectToEncode);
        var bytes = await GetBytes(content);
        var header = bytes[..4];
        var headerValue = BitConverter.ToInt32(header);
        var end = 8 + headerValue;
        var parsedContent = bytes[8..end];
        parsedContent.Length.Should().Be(headerValue);
        var slack = bytes[end..];
        slack.Should().AllBeEquivalentTo(byte.MinValue);
    }
    
    [Fact]
    public async Task HeaderHash_MatchesContentHash()
    {
        var objectToEncode = GetRandomBytes();
        await using var content = await GetStream(objectToEncode);
        var bytes = await GetBytes(content);
        var hashHeader = bytes[4..8];
        var hashValue = BitConverter.ToInt32(hashHeader);
        var sizeHeader = bytes[..4];
        var sizeValue = BitConverter.ToInt32(sizeHeader);
        var end = 8 + sizeValue;
        var parsedContent = bytes[8..end];
        var parsedContentHash = ContrabandEncoder.GetHashFromBytes(parsedContent);
        parsedContentHash.Should().Be(hashValue);
    }

    [Fact]
    public async Task Encode_ReturnsVideoStream()
    {
        var content = GetRandomBytes();
        var engine = new Engine("C:\\ProgramData\\chocolatey\\bin\\ffmpeg.exe");
        var encoder = new ContrabandEncoder(engine);
        var stream = await encoder.GenerateVideo(content);
        stream.CanRead.Should().BeTrue();
    }

    protected static async Task<Stream> GetStream(byte[] content)
    {
        var image = new ContrabandEncoder(null!).GenerateFrame(content);
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

    protected static byte[] GetRandomBytes()
    {
        var length = Random.Shared.Next(20, 1000);
        var buffer = new byte[length];
        Random.Shared.NextBytes(buffer);
        return buffer;
    }

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
}