namespace Prompt.Tests;

using System;
using System.IO;
using System.Text.Json;
using Xunit;

/// <summary>
/// Tests for <see cref="SerializationGuards"/> — shared JSON options,
/// payload size limits, and file size guards.
/// </summary>
public class SerializationGuardsTests : IDisposable
{
    private readonly string _tempDir;

    public SerializationGuardsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"prompt-guards-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── MaxJsonPayloadBytes ─────────────────────────────────────────

    [Fact]
    public void MaxJsonPayloadBytes_Is10MB()
    {
        Assert.Equal(10 * 1024 * 1024, SerializationGuards.MaxJsonPayloadBytes);
    }

    // ── ThrowIfPayloadTooLarge ──────────────────────────────────────

    [Fact]
    public void ThrowIfPayloadTooLarge_SmallPayload_DoesNotThrow()
    {
        var json = new string('x', 100);
        SerializationGuards.ThrowIfPayloadTooLarge(json);
        // Should not throw
    }

    [Fact]
    public void ThrowIfPayloadTooLarge_ExactlyAtLimit_DoesNotThrow()
    {
        // Build a string exactly at the limit in UTF-8 bytes
        // ASCII chars are 1 byte each
        var json = new string('a', SerializationGuards.MaxJsonPayloadBytes);
        SerializationGuards.ThrowIfPayloadTooLarge(json);
    }

    [Fact]
    public void ThrowIfPayloadTooLarge_OverLimit_ThrowsInvalidOperationException()
    {
        var json = new string('a', SerializationGuards.MaxJsonPayloadBytes + 1);
        var ex = Assert.Throws<InvalidOperationException>(
            () => SerializationGuards.ThrowIfPayloadTooLarge(json));
        Assert.Contains("maximum allowed size", ex.Message);
        Assert.Contains("10 MB", ex.Message);
    }

    [Fact]
    public void ThrowIfPayloadTooLarge_MultiByte_CountsUtf8Bytes()
    {
        // Each emoji is 4 UTF-8 bytes — so MaxJsonPayloadBytes/4 emojis = at limit
        int emojiCount = SerializationGuards.MaxJsonPayloadBytes / 4;
        // One more emoji pushes over
        var json = new string('🔥', emojiCount + 1);
        Assert.Throws<InvalidOperationException>(
            () => SerializationGuards.ThrowIfPayloadTooLarge(json));
    }

    [Fact]
    public void ThrowIfPayloadTooLarge_EmptyString_DoesNotThrow()
    {
        SerializationGuards.ThrowIfPayloadTooLarge("");
    }

    // ── ThrowIfFileTooLarge ─────────────────────────────────────────

    [Fact]
    public void ThrowIfFileTooLarge_SmallFile_DoesNotThrow()
    {
        var path = Path.Combine(_tempDir, "small.json");
        File.WriteAllText(path, "{}");
        SerializationGuards.ThrowIfFileTooLarge(path);
    }

    [Fact]
    public void ThrowIfFileTooLarge_OversizedFile_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(_tempDir, "big.json");
        // Write a file slightly over the limit
        using (var fs = new FileStream(path, FileMode.Create))
        {
            fs.SetLength(SerializationGuards.MaxJsonPayloadBytes + 1);
        }
        var ex = Assert.Throws<InvalidOperationException>(
            () => SerializationGuards.ThrowIfFileTooLarge(path));
        Assert.Contains("exceeding the maximum allowed size", ex.Message);
    }

    [Fact]
    public void ThrowIfFileTooLarge_ExactlyAtLimit_DoesNotThrow()
    {
        var path = Path.Combine(_tempDir, "exact.json");
        using (var fs = new FileStream(path, FileMode.Create))
        {
            fs.SetLength(SerializationGuards.MaxJsonPayloadBytes);
        }
        SerializationGuards.ThrowIfFileTooLarge(path);
    }

    // ── ReadCamelCase ───────────────────────────────────────────────

    [Fact]
    public void ReadCamelCase_UsesCamelCaseNaming()
    {
        Assert.Equal(JsonNamingPolicy.CamelCase,
            SerializationGuards.ReadCamelCase.PropertyNamingPolicy);
    }

    [Fact]
    public void ReadCamelCase_CanDeserializeCamelCaseJson()
    {
        var json = """{"userName":"Alice","age":30}""";
        var result = JsonSerializer.Deserialize<TestPerson>(json, SerializationGuards.ReadCamelCase);
        Assert.NotNull(result);
        Assert.Equal("Alice", result!.UserName);
        Assert.Equal(30, result.Age);
    }

    // ── WriteIndentedSkipNull ───────────────────────────────────────

    [Fact]
    public void WriteIndentedSkipNull_IndentsOutput()
    {
        var obj = new { Name = "Bob" };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteIndentedSkipNull);
        Assert.Contains("\n", json);
    }

    [Fact]
    public void WriteIndentedSkipNull_SkipsNullProperties()
    {
        var obj = new TestPerson { UserName = "Alice", NickName = null, Age = 25 };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteIndentedSkipNull);
        Assert.DoesNotContain("NickName", json);
        Assert.Contains("Alice", json);
    }

    // ── WriteIndented ───────────────────────────────────────────────

    [Fact]
    public void WriteIndented_IndentsOutput()
    {
        var obj = new { X = 1 };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteIndented);
        Assert.Contains("\n", json);
    }

    [Fact]
    public void WriteIndented_DoesNotSkipNulls()
    {
        var obj = new TestPerson { UserName = "Bob", NickName = null, Age = 20 };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteIndented);
        Assert.Contains("NickName", json);
    }

    // ── WriteCamelCase ──────────────────────────────────────────────

    [Fact]
    public void WriteCamelCase_UsesCamelCaseNaming()
    {
        Assert.Equal(JsonNamingPolicy.CamelCase,
            SerializationGuards.WriteCamelCase.PropertyNamingPolicy);
    }

    [Fact]
    public void WriteCamelCase_SerializesWithCamelCase()
    {
        var obj = new TestPerson { UserName = "Charlie", Age = 40 };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteCamelCase);
        Assert.Contains("userName", json);
        Assert.DoesNotContain("UserName", json);
    }

    [Fact]
    public void WriteCamelCase_IsIndented()
    {
        var obj = new { X = 1 };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteCamelCase);
        Assert.Contains("\n", json);
    }

    [Fact]
    public void WriteCamelCase_SkipsNulls()
    {
        var obj = new TestPerson { UserName = "Dave", NickName = null, Age = 35 };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteCamelCase);
        Assert.DoesNotContain("nickName", json);
    }

    // ── WriteCompactCamelCase ───────────────────────────────────────

    [Fact]
    public void WriteCompactCamelCase_IsNotIndented()
    {
        var obj = new TestPerson { UserName = "Eve", Age = 28 };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteCompactCamelCase);
        Assert.DoesNotContain("\n", json);
    }

    [Fact]
    public void WriteCompactCamelCase_UsesCamelCase()
    {
        var obj = new TestPerson { UserName = "Eve", Age = 28 };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteCompactCamelCase);
        Assert.Contains("userName", json);
    }

    [Fact]
    public void WriteCompactCamelCase_SkipsNulls()
    {
        var obj = new TestPerson { UserName = "Eve", NickName = null, Age = 28 };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteCompactCamelCase);
        Assert.DoesNotContain("nickName", json);
    }

    // ── WriteOptions ────────────────────────────────────────────────

    [Fact]
    public void WriteOptions_Indented_ReturnsWriteCamelCase()
    {
        var opts = SerializationGuards.WriteOptions(indented: true);
        Assert.Same(SerializationGuards.WriteCamelCase, opts);
    }

    [Fact]
    public void WriteOptions_NotIndented_ReturnsWriteCompactCamelCase()
    {
        var opts = SerializationGuards.WriteOptions(indented: false);
        Assert.Same(SerializationGuards.WriteCompactCamelCase, opts);
    }

    // ── ReadWithEnums ───────────────────────────────────────────────

    [Fact]
    public void ReadWithEnums_UsesCamelCaseNaming()
    {
        Assert.Equal(JsonNamingPolicy.CamelCase,
            SerializationGuards.ReadWithEnums.PropertyNamingPolicy);
    }

    [Fact]
    public void ReadWithEnums_HasStringEnumConverter()
    {
        Assert.Contains(SerializationGuards.ReadWithEnums.Converters,
            c => c is System.Text.Json.Serialization.JsonStringEnumConverter);
    }

    [Fact]
    public void ReadWithEnums_CanDeserializeStringEnums()
    {
        var json = """{"level":"High"}""";
        var result = JsonSerializer.Deserialize<TestEnumHolder>(json,
            SerializationGuards.ReadWithEnums);
        Assert.NotNull(result);
        Assert.Equal(TestLevel.High, result!.Level);
    }

    // ── WriteWithEnums ──────────────────────────────────────────────

    [Fact]
    public void WriteWithEnums_IsIndented()
    {
        var obj = new TestEnumHolder { Level = TestLevel.Low };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteWithEnums);
        Assert.Contains("\n", json);
    }

    [Fact]
    public void WriteWithEnums_SerializesEnumsAsStrings()
    {
        var obj = new TestEnumHolder { Level = TestLevel.Medium };
        var json = JsonSerializer.Serialize(obj, SerializationGuards.WriteWithEnums);
        Assert.Contains("Medium", json);
        Assert.DoesNotContain("1", json); // numeric value
    }

    // ── Shared instance stability ───────────────────────────────────

    [Fact]
    public void SharedOptions_AreSingletonInstances()
    {
        // Verifies the static readonly fields return the same instance each time
        var a = SerializationGuards.ReadCamelCase;
        var b = SerializationGuards.ReadCamelCase;
        Assert.Same(a, b);

        var c = SerializationGuards.WriteCamelCase;
        var d = SerializationGuards.WriteCamelCase;
        Assert.Same(c, d);
    }

    // ── Test models ─────────────────────────────────────────────────

    private class TestPerson
    {
        public string UserName { get; set; } = "";
        public string? NickName { get; set; }
        public int Age { get; set; }
    }

    private enum TestLevel { Low, Medium, High }

    private class TestEnumHolder
    {
        public TestLevel Level { get; set; }
    }
}
