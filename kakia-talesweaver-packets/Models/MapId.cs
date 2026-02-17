namespace kakia_talesweaver_packets.Models;

public class MapId
{
	public ushort Id { get; set; }
	public ushort Zone { get; set; }


	public MapId()
	{
		Id = 0;
		Zone = 0;
	}

	public MapId(ushort id, ushort zone)
	{
		Id = id;
		Zone = zone;
	}

	public override bool Equals(object? obj)
	{
		if (obj == null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;

		var other = obj as MapId;
		return this.Id == other!.Id && this.Zone == other.Zone;
	}

	public static bool operator ==(MapId? left, MapId? right)
	{
		if (left is null && right is null) return true;
		if (left is null || right is null) return false;
		return left.Equals(right);
	}

	public static bool operator !=(MapId? left, MapId? right)
	{
		return !(left == right);
	}

	public override string ToString()
	{
		return $"{Id}-{Zone}";
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}
