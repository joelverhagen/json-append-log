using System.Buffers;
using System.Text.Json;
using DiffEngine;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace JsonLog.Utility;

public static class JsonUtility
{
    private static readonly ArrayPool<byte> TrashPool = ArrayPool<byte>.Create();

    public static long GetJsonSize<T>(T value, JsonSerializerOptions options)
    {
        var writer = new Utf8JsonWriter(NullBufferWriter.Instance);
        JsonSerializer.Serialize(writer, value, options);
        return writer.BytesCommitted;
    }

    private class NullBufferWriter : IBufferWriter<byte>
    {
        public static NullBufferWriter Instance { get; } = new();

        public void Advance(int count)
        {
        }

        public Memory<byte> GetMemory(int sizeHint = 0) => GetTrashBuffer(sizeHint);

        public Span<byte> GetSpan(int sizeHint = 0) => GetTrashBuffer(sizeHint);

        private static byte[] GetTrashBuffer(int sizeHint)
        {
            var buffer = TrashPool.Rent(sizeHint == 0 ? 1024 : sizeHint);
            TrashPool.Return(buffer);
            return buffer;
        }
    }

    public static void VerifyRoundTrip<T>(string originalJson, T deserialized, JsonSerializerOptions options)
    {
        var serializedJson = JsonSerializer.Serialize(deserialized, options);
        if (originalJson != serializedJson)
        {
            var originalJsonRoundTrip = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonDocument>(originalJson, options), options);
            if (originalJsonRoundTrip != originalJson)
            {
                throw new JsonException("The original JSON should be round-trippable.");
            }

            var indentedOptions = new JsonSerializerOptions(options);
            indentedOptions.WriteIndented = true;

            var originalJsonIndented = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonDocument>(originalJson, indentedOptions), indentedOptions);
            var serializedJsonIndented = JsonSerializer.Serialize(deserialized, indentedOptions);

            var diff = InlineDiffBuilder.Diff(originalJsonIndented, serializedJsonIndented);
            var changedCount = diff.Lines.Count(line => line.Type != ChangeType.Unchanged);

            var originalTemp = Path.GetTempFileName();
            var serializedTemp = Path.GetTempFileName();
            try
            {
                if (changedCount == 0)
                {
                    File.WriteAllText(originalTemp, originalJson);
                    File.WriteAllText(serializedTemp, serializedJson);
                }
                else
                {
                    File.WriteAllText(originalTemp, originalJsonIndented);
                    File.WriteAllText(serializedTemp, serializedJsonIndented);
                }

                var launchResult = DiffRunner.Launch(DiffTool.BeyondCompare, originalTemp, serializedTemp);
                if (launchResult == LaunchResult.NoDiffToolFound)
                {
                    throw new JsonException("No diff tool found.");
                }
            }
            finally
            {
                File.Delete(originalTemp);
                File.Delete(serializedTemp);
            }

            throw new JsonException("The deserialized modle should serialize to the same string.");
        }
    }
}
