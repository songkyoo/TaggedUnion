using MemoryPack;

namespace Macaron.Union.Tests;

public readonly struct ManualMemoryPackTestValue : IMemoryPackable<ManualMemoryPackTestValue>
{
    public ManualMemoryPackTestValue(int value)
    {
        Value = value;
    }

    public int Value { get; }

    static void IMemoryPackFormatterRegister.RegisterFormatter()
    {
        if (!MemoryPackFormatterProvider.IsRegistered<ManualMemoryPackTestValue>())
        {
            MemoryPackFormatterProvider.Register(
                new MemoryPack.Formatters.MemoryPackableFormatter<ManualMemoryPackTestValue>()
            );
        }
    }

    static void IMemoryPackable<ManualMemoryPackTestValue>.Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer,
        scoped ref ManualMemoryPackTestValue value
    )
    {
        writer.WriteValue(value.Value);
    }

    static void IMemoryPackable<ManualMemoryPackTestValue>.Deserialize(
        ref MemoryPackReader reader,
        scoped ref ManualMemoryPackTestValue value
    )
    {
        value = new ManualMemoryPackTestValue(reader.ReadValue<int>());
    }
}
