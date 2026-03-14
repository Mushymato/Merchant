using Merchant.Management;
using Merchant.Models;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Pathfinding;

namespace Merchant.Misc;

public sealed class PathingLocation(GameLocation location, List<CustomerActor> dispatchedActors) : GameLocation
{
    public override bool isCollidingPosition(
        Rectangle position,
        xTile.Dimensions.Rectangle viewport,
        bool isFarmer,
        int damagesFarmer,
        bool glider,
        Character character
    )
    {
        location.isCollidingPosition(position, viewport, isFarmer, damagesFarmer, glider, character);
        foreach (CustomerActor actor in dispatchedActors)
        {
            if (!actor.IsInvisible && actor.TilePoint == character.TilePoint && actor.isMoving())
            {
                return actor.Name.CompareTo(character.Name) < 0;
            }
        }
        return false;
    }
}

public sealed record LocationTopology(Point EntryPoint, HashSet<Point> ReachablePoints);

public static class Topology
{
    public static List<(Point, int)> FormBrowseAround(Rectangle boundingBox, HashSet<Point> reachable)
    {
        List<(Point, int)> browseAround = [];
        Point pnt;
        for (int i = 0; i < boundingBox.Width; i++)
        {
            int x = boundingBox.Left + i;
            // X
            // .
            pnt = new(x, boundingBox.Bottom);
            if (reachable.Contains(pnt))
                browseAround.Add(new(pnt, 0));
            // .
            // X
            pnt = new(x, boundingBox.Top - 1);
            if (reachable.Contains(pnt))
                browseAround.Add(new(pnt, 2));
        }
        for (int i = 0; i < boundingBox.Height; i++)
        {
            int y = boundingBox.Top + i;
            // .X
            pnt = new(boundingBox.Left - 1, y);
            if (reachable.Contains(pnt))
                browseAround.Add(new(pnt, 1));
            // X.
            pnt = new(boundingBox.Right, y);
            if (reachable.Contains(pnt))
                browseAround.Add(new(pnt, 3));
        }
        return browseAround;
    }

    internal static IEnumerable<Point> Surrounding4Tiles(Point nextPoint, int maxX, int maxY)
    {
        if (nextPoint.X > 0)
            yield return new(nextPoint.X - 1, nextPoint.Y);
        if (nextPoint.Y > 0)
            yield return new(nextPoint.X, nextPoint.Y - 1);
        if (nextPoint.X < maxX - 1)
            yield return new(nextPoint.X + 1, nextPoint.Y);
        if (nextPoint.Y < maxY - 1)
            yield return new(nextPoint.X, nextPoint.Y + 1);
    }

    internal static IEnumerable<Point> Surrounding8Tiles(Point nextPoint, int maxX, int maxY)
    {
        bool canLeft = nextPoint.X > 0;
        bool canRight = nextPoint.X < maxX - 1;
        bool canUp = nextPoint.Y > 0;
        bool canDown = nextPoint.Y < maxY - 1;
        // cardinal
        if (canLeft)
            yield return new(nextPoint.X - 1, nextPoint.Y);
        if (canUp)
            yield return new(nextPoint.X, nextPoint.Y - 1);
        if (canRight)
            yield return new(nextPoint.X + 1, nextPoint.Y);
        if (canDown)
            yield return new(nextPoint.X, nextPoint.Y + 1);
        // other tiles
        if (canLeft)
        {
            if (canUp)
                yield return new(nextPoint.X - 1, nextPoint.Y - 1);
            if (canDown)
                yield return new(nextPoint.X - 1, nextPoint.Y + 1);
        }
        if (canRight)
        {
            if (canUp)
                yield return new(nextPoint.X + 1, nextPoint.Y - 1);
            if (canDown)
                yield return new(nextPoint.X + 1, nextPoint.Y + 1);
        }
    }

    internal static bool IsTileStandable(
        GameLocation location,
        Point tile,
        CollisionMask collisionMask = ~(CollisionMask.Characters | CollisionMask.Farmers)
    )
    {
        return IsTilePassable(location, tile)
            && !IsWarp(location, tile)
            && !location.IsTileBlockedBy(
                tile.ToVector2(),
                collisionMask: collisionMask,
                ignorePassables: CollisionMask.All
            );
    }

    /// <summary>Get whether players can walk on a map tile.</summary>
    /// <param name="location">The location to check.</param>
    /// <param name="tile">The tile position.</param>
    /// <remarks>This is derived from <see cref="GameLocation.isTilePassable(Vector2)" />, but also checks tile properties in addition to tile index properties to match the actual game behavior.</remarks>
    /// <remarks>Originally written for DataLayers</remarks>
    private static bool IsTilePassable(GameLocation location, Point tile)
    {
        // passable if Buildings layer has 'Passable' property
        xTile.Tiles.Tile? buildingTile = location.map.RequireLayer("Buildings").Tiles[(int)tile.X, (int)tile.Y];
        if (buildingTile?.Properties.ContainsKey("Passable") is true)
            return true;

        // non-passable if Back layer has 'Passable' property
        xTile.Tiles.Tile? backTile = location.map.RequireLayer("Back").Tiles[(int)tile.X, (int)tile.Y];
        if (backTile?.Properties.ContainsKey("Passable") is true)
            return false;

        // else check tile indexes
        return location.isTilePassable(tile.ToVector2());
    }

    private static bool IsWarp(GameLocation location, Point tile)
    {
        if (location.warps.Any(warp => warp.X == tile.X && warp.Y == tile.Y))
        {
            return true;
        }
        if (location.doors.ContainsKey(tile))
        {
            return true;
        }
        if (location.doesTileHaveProperty(tile.X, tile.Y, "TouchAction", "Back") is string touchAction)
        {
            return touchAction == "Warp" || touchAction == "MagicWarp";
        }
        return false;
    }

    internal static HashSet<Point> TileStandableBFS(
        GameLocation location,
        Point startingTile,
        CollisionMask collisionMask = ~CollisionMask.Characters
    )
    {
        int maxX = location.Map.DisplayWidth / 64;
        int maxY = location.Map.DisplayHeight / 64;
        Dictionary<Point, bool> tileStandableState = [];
        tileStandableState[startingTile] = IsTileStandable(location, startingTile, collisionMask);
        Queue<(Point, int)> tileQueue = [];
        tileQueue.Enqueue(new(startingTile, 0));
        int standableCnt = 1;
        while (tileQueue.Count > 0)
        {
            (Point, int) next = tileQueue.Dequeue();
            Point nextPoint = next.Item1;
            int depth = next.Item2 + 1;
            foreach (Point neighbour in Surrounding4Tiles(nextPoint, maxX, maxY))
            {
                if (!tileStandableState.ContainsKey(neighbour))
                {
                    bool standable = IsTileStandable(location, neighbour, collisionMask);
                    tileStandableState[neighbour] = standable;
                    if (standable)
                    {
                        standableCnt++;
                        tileQueue.Enqueue(new(neighbour, depth));
                    }
                }
            }
        }
        return tileStandableState.Where(kv => kv.Value).Select(kv => kv.Key).ToHashSet();
    }

    public static readonly sbyte[,] directions = new sbyte[4, 2]
    {
        { -1, 0 },
        { 1, 0 },
        { 0, 1 },
        { 0, -1 },
    };

    // StardewValley.Pathfinding.PathFindController.findPath
    public static Stack<Point>? FindPath(
        HashSet<Point> reachablePoints,
        Point startPoint,
        Point endPoint,
        GameLocation location,
        Character character,
        int limit
    )
    {
        bool flag = character is FarmAnimal farmAnimal && farmAnimal.CanSwim() && farmAnimal.isSwimming.Value;
        PriorityQueue openList = new();
        HashSet<int> closedList = [];

        int num = 0;
        openList.Enqueue(
            new PathNode(startPoint.X, startPoint.Y, 0, null),
            Math.Abs(endPoint.X - startPoint.X) + Math.Abs(endPoint.Y - startPoint.Y)
        );
        int layerWidth = location.map.Layers[0].LayerWidth;
        int layerHeight = location.map.Layers[0].LayerHeight;
        while (!openList.IsEmpty())
        {
            PathNode pathNode = openList.Dequeue();
            if (PathFindController.isAtEndPoint(pathNode, endPoint, location, character))
            {
                return PathFindController.reconstructPath(pathNode);
            }
            closedList.Add(pathNode.id);
            int num2 = (byte)(pathNode.g + 1);
            for (int i = 0; i < 4; i++)
            {
                int num3 = pathNode.x + directions[i, 0];
                int num4 = pathNode.y + directions[i, 1];
                int item = PathNode.ComputeHash(num3, num4);
                if (closedList.Contains(item))
                {
                    continue;
                }
                if (
                    (num3 != endPoint.X || num4 != endPoint.Y)
                    && (num3 < 0 || num4 < 0 || num3 >= layerWidth || num4 >= layerHeight)
                )
                {
                    closedList.Add(item);
                    continue;
                }
                PathNode pathNode2 = new(num3, num4, pathNode) { g = (byte)(pathNode.g + 1) };
                if (!flag && !reachablePoints.Contains(new(num3, num4)))
                {
                    closedList.Add(item);
                    continue;
                }
                int priority = num2 + Math.Abs(endPoint.X - num3) + Math.Abs(endPoint.Y - num4);
                closedList.Add(item);
                openList.Enqueue(pathNode2, priority);
            }
            num++;
            if (num >= limit)
            {
                return null;
            }
        }
        return null;
    }

    public static bool TileIsAdjacentToMerchantObject(GameLocation location, Point pointToCheck, int maxX, int maxY)
    {
        foreach (Point pnt in Surrounding8Tiles(pointToCheck, maxX, maxY))
        {
            if (
                location.objects.TryGetValue(pnt.ToVector2(), out SObject obj)
                && obj.GetMachineData().InteractMethod is string interactMethod
                && (
                    interactMethod == GameDelegates.InteractMethod_CashRegister
                    || interactMethod == GameDelegates.InteractMethod_RoboShopkeep
                )
            )
            {
                return true;
            }
        }
        return false;
    }
}
