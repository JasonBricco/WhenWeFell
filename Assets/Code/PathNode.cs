﻿//
// When We Fell
//

using System;
using UnityEngine;

public sealed class PathNode : IEquatable<PathNode>, IComparable<PathNode>
{
	public Vector2Int pos;
	public int g, f, h;
	public PathNode parent;

	public int CompareTo(PathNode other)
	{
		return f.CompareTo(other.f);
	}

	public bool Equals(PathNode other)
		=> pos == other.pos;

	public override string ToString()
	{
		return pos.ToString() + ", F: " + f;
	}
}