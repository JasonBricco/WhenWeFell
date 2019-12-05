﻿//
// When We Fell
// 

using UnityEngine;
using System.Collections.Generic;

public class World : MonoBehaviour
{
	// Chunks are stored in a hash map for maximum flexibility. This doesn't force 
	// any constraints as to how the world should be.
	// Accessing chunks is slightly slower, but the difference is negligible.
	private Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();

	private void Start()
	{
		//SampleRoomLoader generator = new SampleRoomLoader();
		ProcGen generator = new ProcGen();
		generator.Generate(this);
	}

	// Returns the chunk at the given position, or null if the
	// chunk doesn't exist.
	public Chunk GetChunk(int cX, int cY)
	{
		if (chunks.TryGetValue(new Vector2Int(cX, cY), out Chunk chunk))
			return chunk;

		return null;
	}

	public Chunk GetChunk(Vector2Int cP)
		=> GetChunk(cP.x, cP.y);

	public void SetChunk(int cX, int cY, Chunk chunk)
		=> chunks.Add(new Vector2Int(cX, cY), chunk);

	// Returns the tile at the given world location.
	public Tile GetTile(int wX, int wY)
	{
		Vector2Int cP = Utils.WorldToChunkP(wX, wY);
		Chunk chunk = GetChunk(cP);

		if (chunk == null)
			return TileType.Air;

		Vector2Int rel = Utils.WorldToRelP(wX, wY);
		return chunk.GetTile(rel.x, rel.y);
	}

	// Sets a tile at the given world location. Computes the chunk the tile belongs
	// in, and creates it if it doesn't exist.
	public Chunk SetTile(int wX, int wY, Tile tile)
	{
		Vector2Int cP = Utils.WorldToChunkP(wX, wY);
		Chunk chunk = GetChunk(cP);

		if (chunk == null)
		{
			chunk = new Chunk(cP.x, cP.y);
			chunks.Add(cP, chunk);
		}

		Vector2Int rel = Utils.WorldToRelP(wX, wY);
		chunk.SetTile(rel.x, rel.y, tile);

		return chunk;
	}

	public void SetTileArea(AABB bb, Tile tile)
	{
		Vector2Int min = Utils.TilePos(bb.center - bb.radius);
		Vector2Int max = Utils.TilePos(bb.center + bb.radius);

		for (int y = min.y; y <= max.y; ++y)
		{
			for (int x = min.x; x <= max.x; ++x)
				SetTile(x, y, tile);
		}
	}

	public List<Entity> GetOverlappingEntities(AABB bb)
	{
		List<Entity> result = new List<Entity>();

		Vector2Int minChunk = Utils.WorldToChunkP(bb.center - bb.radius);
		Vector2Int maxChunk = Utils.WorldToChunkP(bb.center + bb.radius);

		for (int y = minChunk.y; y <= maxChunk.y; ++y)
		{
			for (int x = minChunk.x; x <= maxChunk.x; ++x)
			{
				Chunk chunk = GetChunk(x, y);

				if (chunk == null)
					continue;

				List<Entity> list = chunk.entities;

				for (int i = 0; i < list.Count; i++)
				{
					Entity targetEntity = list[i];

					if (AABB.TestOverlap(bb, targetEntity.GetBoundingBox()))
						result.Add(targetEntity);
				}
			}
		}

		return result;
	}

	private float TileRayIntersection(Vector2 tilePos, Ray ray)
	{
		float nearP = -float.MaxValue;
		float farP = float.MaxValue;

		for (int i = 0; i < 2; i++)
		{
			float min = tilePos[i];
			float max = tilePos[i] + 1.0f;

			float pos = ray.origin[i];
			float dir = ray.direction[i];

			if (Mathf.Abs(dir) <= Mathf.Epsilon)
			{
				if ((pos < min) || (pos > max))
					return float.MaxValue;
			}

			float t0 = (min - pos) / dir;
			float t1 = (max - pos) / dir;

			if (t0 > t1)
			{
				float tmp = t0;
				t0 = t1;
				t1 = tmp;
			}

			nearP = Mathf.Max(t0, nearP);
			farP = Mathf.Min(t1, farP);

			if (nearP > farP) return float.MaxValue;
			if (farP < 0.0f) return float.MaxValue;
		}

		return nearP > 0.0f ? nearP : farP;
	}

	public bool TileRaycast(Ray ray, float dist, out Vector2 result)
	{
		Vector2Int start = Utils.TilePos(ray.origin);
		Vector2Int end = Utils.TilePos(ray.origin + ray.direction * dist);

		if (start.x > end.x)
		{
			int tmp = start.x;
			start.x = end.x;
			end.x = tmp;
		}

		if (start.y > end.y)
		{
			int tmp = start.y;
			start.y = end.y;
			end.y = tmp;
		}

		float minDistance = dist;

		for (int y = start.y; y <= end.y; y++)
		{
			for (int x = start.x; x <= end.x; x++)
			{
				Tile tile = GetTile(x, y);

				if (TileManager.GetData(tile).passable) continue;

				float newDist = TileRayIntersection(new Vector2(x, y), ray);
				minDistance = Mathf.Min(minDistance, newDist);
			}
		}

		if (minDistance != dist)
		{
			result = ray.origin + ray.direction * minDistance;
			return true;
		}

		result = Vector2.zero;
		return false;
	}

	public void Update()
	{
		if (Debug.isDebugBuild)
		{
			if (Input.GetMouseButton(1))
			{
				Vector2 cursor = Camera.main.ScreenToWorldPoint(Input.mousePosition);
				Vector2Int cursorP = Utils.TilePos(cursor);

				Tile tile = GetTile(cursorP.x, cursorP.y);

				if (tile != TileType.Air && tile != TileType.CaveWall)
				{
					Chunk chunk = SetTile(cursorP.x, cursorP.y, TileType.CaveWall);
					chunk.SetModified();
				}
			}

			if (Input.GetKeyDown(KeyCode.K))
			{
				GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

				for (int i = 0; i < enemies.Length; ++i)
				{
					Entity entity = enemies[i].GetComponent<Entity>();
					entity.Damage(int.MaxValue);
				}
			}
		}
	}
}
