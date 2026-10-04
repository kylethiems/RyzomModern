using System.Buffers.Binary;
using System.Text;
using Ryzom.Core.Math3D;

namespace Ryzom.Core.Network;

public enum RyzomPacketType : byte
{
    Ping = 0x01,
    Pong = 0x02,
    LoginRequest = 0x10,
    LoginResponse = 0x11,
    EntitySpawn = 0x20,
    EntityDespawn = 0x21,
    MovementUpdate = 0x30,
    ActionCast = 0x40,
    CombatDelta = 0x41,
    ChatBroadcast = 0x50
}

public ref struct BitStreamWriter
{
    private readonly Span<byte> _buffer;
    private int _position;

    public BitStreamWriter(Span<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    public int BytesWritten => _position;
    public ReadOnlySpan<byte> WrittenSpan => _buffer[.._position];

    public void WriteByte(byte value)
    {
        _buffer[_position++] = value;
    }

    public void WriteUInt16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.Slice(_position, 2), value);
        _position += 2;
    }

    public void WriteInt32(int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.Slice(_position, 4), value);
        _position += 4;
    }

    public void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.Slice(_position, 4), value);
        _position += 4;
    }

    public void WriteUInt64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer.Slice(_position, 8), value);
        _position += 8;
    }

    public void WriteSingle(float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(_buffer.Slice(_position, 4), value);
        _position += 4;
    }

    public void WriteVector3f(Vector3f v)
    {
        WriteSingle(v.X);
        WriteSingle(v.Y);
        WriteSingle(v.Z);
    }

    public void WriteStringUtf8(string str)
    {
        int byteCount = Encoding.UTF8.GetByteCount(str);
        WriteUInt16((ushort)byteCount);
        Encoding.UTF8.GetBytes(str, _buffer.Slice(_position, byteCount));
        _position += byteCount;
    }
}

public ref struct BitStreamReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _position;

    public BitStreamReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    public int Position => _position;
    public int Remaining => _buffer.Length - _position;

    public byte ReadByte() => _buffer[_position++];

    public ushort ReadUInt16()
    {
        ushort val = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Slice(_position, 2));
        _position += 2;
        return val;
    }

    public int ReadInt32()
    {
        int val = BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(_position, 4));
        _position += 4;
        return val;
    }

    public uint ReadUInt32()
    {
        uint val = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(_position, 4));
        _position += 4;
        return val;
    }

    public ulong ReadUInt64()
    {
        ulong val = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Slice(_position, 8));
        _position += 8;
        return val;
    }

    public float ReadSingle()
    {
        float val = BinaryPrimitives.ReadSingleLittleEndian(_buffer.Slice(_position, 4));
        _position += 4;
        return val;
    }

    public Vector3f ReadVector3f()
    {
        float x = ReadSingle();
        float y = ReadSingle();
        float z = ReadSingle();
        return new Vector3f(x, y, z);
    }

    public string ReadStringUtf8()
    {
        ushort byteCount = ReadUInt16();
        string str = Encoding.UTF8.GetString(_buffer.Slice(_position, byteCount));
        _position += byteCount;
        return str;
    }
}
