using System.Text;
using WfpTrafficControl.Cli;
using WfpTrafficControl.Shared.Ipc;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Unit tests for CLI request serialization.
/// These tests verify that the CLI correctly serializes requests
/// in the format expected by the service pipe server.
/// </summary>
public class CliRequestSerializerTests
{
    [Fact]
    public void SerializePingRequestReturnsCorrectJson()
    {
        var request = new PingRequest();

        var json = CliRequestSerializer.Serialize(request);

        Assert.Equal("""{"type":"ping"}""", json);
    }

    [Fact]
    public void SerializePingRequestContainsTypeField()
    {
        var request = new PingRequest();

        var json = CliRequestSerializer.Serialize(request);

        Assert.Contains("\"type\":", json);
        Assert.Contains("\"ping\"", json);
    }

    [Fact]
    public void SerializeToWireFormatPingRequestHasCorrectLengthPrefix()
    {
        var request = new PingRequest();

        var wireFormat = CliRequestSerializer.SerializeToWireFormat(request);

        // First 4 bytes should be the length prefix (little-endian)
        var lengthPrefix = BitConverter.ToInt32(wireFormat, 0);

        // The JSON for ping request is {"type":"ping"} which is 15 bytes
        Assert.Equal(15, lengthPrefix);
    }

    [Fact]
    public void SerializeToWireFormatPingRequestHasCorrectTotalLength()
    {
        var request = new PingRequest();

        var wireFormat = CliRequestSerializer.SerializeToWireFormat(request);

        // Total length should be 4 (length prefix) + 15 (JSON body) = 19 bytes
        Assert.Equal(19, wireFormat.Length);
    }

    [Fact]
    public void SerializeToWireFormatPingRequestHasCorrectJsonBody()
    {
        var request = new PingRequest();

        var wireFormat = CliRequestSerializer.SerializeToWireFormat(request);

        // Extract the JSON body (skip first 4 bytes)
        var jsonBytes = new byte[wireFormat.Length - 4];
        Buffer.BlockCopy(wireFormat, 4, jsonBytes, 0, jsonBytes.Length);
        var json = Encoding.UTF8.GetString(jsonBytes);

        Assert.Equal("""{"type":"ping"}""", json);
    }

    [Fact]
    public void SerializeToWireFormatPingRequestLengthPrefixMatchesBodyLength()
    {
        var request = new PingRequest();

        var wireFormat = CliRequestSerializer.SerializeToWireFormat(request);

        // Get length prefix
        var lengthPrefix = BitConverter.ToInt32(wireFormat, 0);

        // Body length should match
        var bodyLength = wireFormat.Length - 4;
        Assert.Equal(bodyLength, lengthPrefix);
    }

    [Fact]
    public void SerializeToWireFormatPingRequestIsLittleEndian()
    {
        var request = new PingRequest();

        var wireFormat = CliRequestSerializer.SerializeToWireFormat(request);

        // For a 15-byte JSON body, the length prefix should be:
        // Little-endian: 0F 00 00 00
        Assert.Equal(0x0F, wireFormat[0]); // LSB first
        Assert.Equal(0x00, wireFormat[1]);
        Assert.Equal(0x00, wireFormat[2]);
        Assert.Equal(0x00, wireFormat[3]);
    }

    [Fact]
    public void SerializePingRequestMatchesServerExpectedFormat()
    {
        // This test verifies that the CLI serialization matches
        // what the server's IpcMessageParser expects
        var request = new PingRequest();

        var json = CliRequestSerializer.Serialize(request);

        // Parse using the server's parser to verify compatibility
        var parseResult = IpcMessageParser.ParseRequest(json);

        Assert.True(parseResult.IsSuccess);
        Assert.IsType<PingRequest>(parseResult.Value);
    }

    [Fact]
    public void SerializeToWireFormatPingRequestBodyIsValidUtf8()
    {
        var request = new PingRequest();

        var wireFormat = CliRequestSerializer.SerializeToWireFormat(request);

        // Extract body and verify it's valid UTF-8
        var bodyBytes = new byte[wireFormat.Length - 4];
        Buffer.BlockCopy(wireFormat, 4, bodyBytes, 0, bodyBytes.Length);

        // Should not throw
        var json = Encoding.UTF8.GetString(bodyBytes);

        // Should be valid JSON
        Assert.StartsWith("{", json);
        Assert.EndsWith("}", json);
    }
}
