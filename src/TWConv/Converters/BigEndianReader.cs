using System.Buffers.Binary;

namespace TalesWeaverMapConverter;

/// <summary>
/// Sequential reader over a byte buffer using Big Endian byte order.
/// Entity .bin files contain captured network packets in BE byte order.
/// </summary>
public ref struct PacketReader
{
	private readonly ReadOnlySpan<byte> _buffer;
	private int _position;

	public PacketReader(ReadOnlySpan<byte> buffer)
	{
		_buffer = buffer;
		_position = 0;
	}

	public int Position => _position;
	public int Remaining => _buffer.Length - _position;
	public int Length => _buffer.Length;

	public void Seek(int position) => _position = position;
	public void Skip(int count) => _position += count;

	public byte ReadByte() => _buffer[_position++];
	public sbyte ReadSByte() => (sbyte)_buffer[_position++];

	public ushort ReadUInt16()
	{
		var value = BinaryPrimitives.ReadUInt16BigEndian(_buffer[_position..]);
		_position += 2;
		return value;
	}

	public short ReadInt16()
	{
		var value = BinaryPrimitives.ReadInt16BigEndian(_buffer[_position..]);
		_position += 2;
		return value;
	}

	public uint ReadUInt32()
	{
		var value = BinaryPrimitives.ReadUInt32BigEndian(_buffer[_position..]);
		_position += 4;
		return value;
	}

	public int ReadInt32()
	{
		var value = BinaryPrimitives.ReadInt32BigEndian(_buffer[_position..]);
		_position += 4;
		return value;
	}

	public long ReadInt64()
	{
		var value = BinaryPrimitives.ReadInt64BigEndian(_buffer[_position..]);
		_position += 8;
		return value;
	}

	public ulong ReadUInt64()
	{
		var value = BinaryPrimitives.ReadUInt64BigEndian(_buffer[_position..]);
		_position += 8;
		return value;
	}

	public byte[] ReadBytes(int count)
	{
		var data = _buffer.Slice(_position, count).ToArray();
		_position += count;
		return data;
	}

	/// <summary>Read a Position struct (2× short BE: X, Y).</summary>
	public (short X, short Y) ReadPosition()
	{
		var x = ReadInt16();
		var y = ReadInt16();
		return (x, y);
	}

	public byte PeekByte() => _buffer[_position];
}