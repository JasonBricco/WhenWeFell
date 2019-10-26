﻿//
// When We Fell
//

using UnityEngine;
using System.Collections.Generic;
using System;
using Object = UnityEngine.Object;

public struct CollideResult
{
	public AABB bb;
	public Entity entity;
	public Tile tile;

	public CollideResult(AABB bb, Entity entity)
	{
		this.bb = bb;
		this.entity = entity;
		tile = default;
	}

	public CollideResult(AABB bb, Tile tile)
	{
		this.bb = bb;
		this.tile = tile;
		entity = null;
	}
}

public class Entity : MonoBehaviour
{
	public const float Epsilon = 0.0001f;

	public float speed;
	public Vector2 size;

	protected Vector2 velocity;
	private float friction = -16.0f;

	protected CollisionFlags colFlags;

	private SpriteRenderer rend;
	private Transform t;

	// Possible collisions and overlaps can be shared between all entities since only one entity will 
	// be using them at a time - this saves memory.
	private static List<CollideResult> possibleCollides = new List<CollideResult>();

	private Comparison<CollideResult> collideCompare;

	public Chunk chunk;

	protected static World world;

	public Vector2 Position
	{
		get { return t.position; }
		set { t.position = value; }
	}

	private void Awake()
	{
		if (world == null)
			world = GameObject.FindWithTag("Manager").GetComponent<World>();

		t = GetComponent<Transform>();
		rend = GetComponent<SpriteRenderer>();

		collideCompare = (CollideResult a, CollideResult b) =>
		{
			float distA = Vector2.SqrMagnitude(Position - a.bb.center);
			float distB = Vector2.SqrMagnitude(Position - b.bb.center);
			return distA < distB ? -1 : 1;
		};
	}

	private void SetFacingDirection()
	{
		if (velocity.x < -Mathf.Epsilon)
			rend.flipX = true;
		else if (velocity.x > Mathf.Epsilon)
			rend.flipX = false;
	}

	public void MoveTo(float x, float y)
	{
		Vector2 p = new Vector2(x, y);
		t.position = p;
		Position = p;
	}

	public void MoveBy(float x, float y)
	{
		Vector2 p = t.position;
		p += new Vector2(x, y);
		t.position = p;
		Position = p;
	}

	public void MoveBy(Vector2 p)
		=> MoveBy(p.x, p.y);

	public void ApplyForce(Vector2 force)
		=> velocity = force;

	public void ApplyForce(float x, float y)
		=> velocity = new Vector2(x, y);

	public AABB GetBoundingBox()
		=> AABB.FromBottomCenter(Position, size);

	protected virtual void OnCollide(CollideResult result) { }

	private void GetPossibleCollidingTiles(World world, AABB entityBB, Vector2Int min, Vector2Int max)
	{
		for (int y = min.y; y <= max.y; ++y)
		{
			for (int x = min.x; x <= max.x; ++x)
			{
				Tile tile = world.GetTile(x, y);
				TileData tileData = TileManager.GetData(tile);

				if (!tileData.passable)
				{
					AABB bb = AABB.FromCorner(new Vector2(x, y), Vector2.one);
					possibleCollides.Add(new CollideResult(bb, tile));
				}
			}
		}
	}

	private void GetPossibleCollidingEntities(World world, AABB entityBB, Vector2Int min, Vector2Int max)
	{
		AABB bounds = AABB.FromMinMax(min, max);

		Vector2Int minChunk = Utils.WorldToChunkP(min.x, min.y);
		Vector2Int maxChunk = Utils.WorldToChunkP(max.x, max.y);

		for (int y = minChunk.y; y <= maxChunk.y; ++y)
		{
			for (int x = minChunk.x; x <= maxChunk.x; ++x)
			{
				Chunk chunk = world.GetChunk(x, y);

				if (chunk == null)
					continue;

				List<Entity> list = chunk.entities;

				for (int i = 0; i < list.Count; i++)
				{
					Entity targetEntity = list[i];

					if (!Physics2D.GetIgnoreLayerCollision(gameObject.layer, targetEntity.gameObject.layer))
					{
						if (AABB.TestOverlap(bounds, entityBB))
						{
							CollideResult info = new CollideResult(targetEntity.GetBoundingBox(), targetEntity);
							possibleCollides.Add(info);
						}
					}
				}
			}
		}
	}

	private void Rebase(World world)
	{
		Vector2Int cP = Utils.WorldToChunkP(Position);

		if (chunk == null)
		{
			Chunk newChunk = world.GetChunk(cP.x, cP.y);

			if (newChunk != null)
				world.GetChunk(cP.x, cP.y).SetEntity(this);
		}
		else if (cP != chunk.cPos)
		{
			chunk.RemoveEntity(this);
			world.GetChunk(cP.x, cP.y).SetEntity(this);
		}
	}

	public void Move(World world, Vector2 accel, float gravity)
	{
		accel *= speed;
		accel += velocity * friction;

		if (gravity != 0.0f)
			accel.y = gravity;

		// Using the following equations of motion:

		// - p' = 1/2at^2 + vt + p.
		// - v' = at + v.
		// - a = specified by input.

		// Where a = acceleration, v = velocity, and p = position.
		// v' and p' denote new versions, while non-prime denotes old.

		// These are found by integrating up from acceleration to velocity. Use derivation
		// to go from position down to velocity and then down to acceleration to see how 
		// we can integrate back up.
		Vector2 delta = accel * 0.5f * Utils.Square(Time.deltaTime) + velocity * Time.deltaTime;
		velocity = accel * Time.deltaTime + velocity;

		Vector2 target = Position + delta;
		AABB entityBB = GetBoundingBox();

		colFlags = CollisionFlags.None;

		// Player size in tiles.
		Vector2Int tSize = Utils.CeilToInt(entityBB.radius * 2.0f);

		Vector2Int start = Utils.TilePos(Position);
		Vector2Int end = Utils.TilePos(target);

		// Compute the range of tiles we could touch with our movement. We'll test for collisions
		// with the tiles in this range.
		Vector2Int min = new Vector2Int(Mathf.Min(start.x, end.x) - tSize.x, Mathf.Min(start.y, end.y) - tSize.y);
		Vector2Int max = new Vector2Int(Mathf.Max(start.x, end.x) + tSize.x, Mathf.Max(start.y, end.y) + tSize.y);

		// Tile collision checking.
		GetPossibleCollidingTiles(world, entityBB, min, max);

		// Entity collision checking.
		GetPossibleCollidingEntities(world, entityBB, min, max);

		possibleCollides.Sort(collideCompare);

		float tRemaining = 1.0f;

		for (int it = 0; it < 3 && tRemaining > 0.0f; ++it)
		{
			float tMin = 1.0f;
			Vector2 normal = Vector2.zero;

			CollideResult hitResult = default;
			bool hit = false;

			for (int i = 0; i < possibleCollides.Count; ++i)
			{
				CollideResult info = possibleCollides[i];
				bool result = TestTileCollision(world, entityBB, info.bb, delta, ref tMin, ref normal);

				if (result)
				{
					hitResult = info;
					hit = true;
				}
			}

			MoveBy(delta * tMin);

			if (normal != Vector2.zero)
				MoveBy((normal * Epsilon));

			if (hit) OnCollide(hitResult);

			entityBB = GetBoundingBox();

			// Subtract away the component of the velocity that collides with the tile wall 
			// and leave the remaining velocity intact.
			velocity -= Vector2.Dot(velocity, normal) * normal;
			delta -= Vector2.Dot(delta, normal) * normal;

			delta -= (delta * tMin);
			tRemaining -= (tMin * tRemaining);
		}

		possibleCollides.Clear();
		SetFacingDirection();

		Rebase(world);
	}

	private bool TestTileCollision(World world, AABB a, AABB b, Vector2 delta, ref float tMin, ref Vector2 normal)
	{
		bool result = false;

		b.Expand(a.radius);
		a.Shrink(a.radius);

		Vector2Int tPos = Utils.TilePos(b.center);
		Vector2 wMin = b.center - b.radius, wMax = b.center + b.radius;

		Tile up = world.GetTile(tPos.x, tPos.y + 1);
		Tile down = world.GetTile(tPos.x, tPos.y - 1);
		Tile left = world.GetTile(tPos.x - 1, tPos.y);
		Tile right = world.GetTile(tPos.x + 1, tPos.y);

		// Top surface.
		if (TileManager.GetData(up).passable && TestWall(delta, a.center, wMax.y, wMin, wMax, 1, 0, ref tMin))
		{
			normal = Vector2.up;

			if (delta.y < 0.0f)
			{
				colFlags |= CollisionFlags.Below;
				result = true;
			}
		}

		// Bottom surface.
		if (TileManager.GetData(down).passable && TestWall(delta, a.center, wMin.y, wMin, wMax, 1, 0, ref tMin))
		{
			normal = Vector2.down;
			colFlags |= CollisionFlags.Above;
			result = true;
		}

		// Left wall.
		if (TileManager.GetData(left).passable && TestWall(delta, a.center, wMin.x, wMin, wMax, 0, 1, ref tMin))
		{
			normal = Vector2.left;
			colFlags |= CollisionFlags.Sides;
			result = true;
		}

		// Right wall.
		if (TileManager.GetData(right).passable && TestWall(delta, a.center, wMax.x, wMin, wMax, 0, 1, ref tMin))
		{
			normal = Vector2.right;
			colFlags |= CollisionFlags.Sides;
			result = true;
		}

		return result;
	}

	private bool TestWall(Vector2 delta, Vector2 p, float wallP, Vector2 wMin, Vector2 wMax, int axis0, int axis1, ref float tMin)
	{
		if (delta[axis0] != 0.0f)
		{
			float tResult = (wallP - p[axis0]) / delta[axis0];

			if (tResult >= 0.0f && tResult < tMin)
			{
				float o1 = p[axis1] + tResult * delta[axis1];

				if (o1 >= wMin[axis1] && o1 <= wMax[axis1])
				{
					tMin = tResult;
					return true;
				}
			}
		}

		return false;
	}

	public new void Destroy(Object obj, float time)
	{
		if (chunk != null)
		{
			chunk.RemoveEntity(this);
			chunk = null;
			Object.Destroy(gameObject, time);
		}
	}

	public new void Destroy(Object obj)
		=> Destroy(obj, 0.0f);
}