using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;

public static class RoomPath
{
    // Size of actor box collider divided by tile size
    public const float actorSize = 0.8f;

    // Stores coordinates in room grid
    public struct RoomPoint
    {
        public int i { get; set; }
        public int j { get; set; }

        public RoomPoint(int row, int col)
        {
            i = row;
            j = col;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !GetType().Equals(obj.GetType()))
            {
                return false;
            }
            RoomPoint other = (RoomPoint)obj;
            return this.i == other.i && this.j == other.j;
        }

        public override int GetHashCode()
        {
            return (i << 8) | j;
        }

        public static bool operator ==(RoomPoint point1, RoomPoint point2)
        {
            return point1.i == point2.i && point1.j == point2.j;
        }

        public static bool operator !=(RoomPoint point1, RoomPoint point2)
        {
            return !(point1 == point2);
        }

        public static int Distance(RoomPoint point1, RoomPoint point2)
        {
            return Mathf.RoundToInt(Mathf.Sqrt((point1.i - point2.i) * (point1.i - point2.i) + (point1.j - point2.j) * (point1.j - point2.j)));
        }
    }

    // Used to store waypoints of path
    public class Path : List<RoomPoint>
    {
        public int GetPathLength()
        {
            int result = 0;
            for (int i = 0; i < this.Count - 1; ++i)
            {
                result += RoomPoint.Distance(this[i], this[i + 1]);
            }
            return result;
        }
    }

    // Build shortest path from start to end using A* algorithm
    public static Path BuildPath(RoomPoint start, RoomPoint end, byte[,] roomGrid)
    {
        PathNode startNode = new PathNode(start);
        PathNode endNode = new PathNode(end);

        var openList = new SortedSet<PathNode>(new PathNode.PathNodeComparer());
        openList.Add(startNode);

        var closedSet = new HashSet<PathNode>();

        while (openList.Count > 0)
        {
            var curNode = openList.Min;
            openList.Remove(curNode);

            if (curNode == endNode)
            {
                return makePathFromNode(curNode);
            }

            int newFromStartToThis = curNode.fromStartToThis + roomGrid[curNode.point.i, curNode.point.j];

            var neighbors = findNeighbors(curNode, roomGrid);

            foreach (var neighbor in neighbors)
            {
                if (closedSet.Contains(neighbor))
                {
                    continue;
                }

                if (openList.TryGetValue(neighbor, out var openListNeighbor))
                {
                    if (newFromStartToThis < openListNeighbor.fromStartToThis)
                    {
                        openList.Remove(openListNeighbor);
                        openListNeighbor.fromStartToThis = newFromStartToThis;
                        openListNeighbor.fromThisToEnd = RoomPoint.Distance(openListNeighbor.point, end);
                        openListNeighbor.fullPath = newFromStartToThis + openListNeighbor.fromThisToEnd;
                        openListNeighbor.parent = curNode;
                        openList.Add(openListNeighbor);
                    }
                }
                else
                {
                    neighbor.fromStartToThis = newFromStartToThis;
                    neighbor.fromThisToEnd = RoomPoint.Distance(neighbor.point, end);
                    neighbor.fullPath = newFromStartToThis + neighbor.fromThisToEnd;
                    neighbor.parent = curNode;
                    openList.Add(neighbor);
                }
            }

            closedSet.Add(curNode);

        }

        return new Path();
    }

    // Build path using local coordinates of start and end points
    public static Path BuildPath(Vector3 start, Vector3 end, byte[,] roomGrid)
    {
        return BuildPath(Room.LocalToRoomPoint(start), Room.LocalToRoomPoint(end), roomGrid);
    }

    // Builds smoothed path by removing redundant waypoints
    public static Path BuildPathSmoothed(RoomPoint start, RoomPoint end, byte[,] roomGrid)
    {
        Path path = BuildPath(start, end, roomGrid);
        return path;
    }

    // Build smoothed path using local coordinates of start and end points
    public static Path BuildPathSmoothed(Vector3 start, Vector3 end, byte[,] roomGrid)
    {
        return BuildPathSmoothed(Room.LocalToRoomPoint(start), Room.LocalToRoomPoint(end), roomGrid);
    }

    // Class used in A* algorithm
    private class PathNode
    {
        public RoomPoint point;

        // Distance from start point to current point
        public int fromStartToThis;

        // Approximate distance from current point to end point
        public int fromThisToEnd;

        // fromStartToThis + fromThisToEnd
        public int fullPath;

        public PathNode parent;

        public PathNode(RoomPoint point)
        {
            this.point = point;
            fromStartToThis = 0;
            fromThisToEnd = 0;
            parent = null;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !GetType().Equals(obj.GetType()))
            {
                return false;
            }
            PathNode other = (PathNode)obj;
            return this.point == other.point;
        }

        public override int GetHashCode()
        {
            return point.GetHashCode();
        }

        public static bool operator ==(PathNode node1, PathNode node2)
        {
            if (node1 is null && node2 is null)
            {
                return true;
            }
            if (node1 is null || node2 is null)
            {
                return false;
            }
            return node1.point == node2.point;
        }

        public static bool operator !=(PathNode node1, PathNode node2)
        {
            return !(node1 == node2);
        }

        public class PathNodeComparer : IComparer<PathNode>
        {
            public int Compare(PathNode node1, PathNode node2)
            {
                if (node1.fullPath < node2.fullPath)
                {
                    return -1;
                }
                if (node1.fullPath > node2.fullPath)
                {
                    return 1;
                }
                return 0;
            }
        }
    }

    // Makes path list from end node of path
    private static Path makePathFromNode(PathNode node)
    {
        var path = new Path();
        while (node != null)
        {
            path.Add(node.point);
            node = node.parent;
        }
        path.Reverse();
        return path;
    }

    // Returns list of nearest nodes that are avaliable for travel
    private static List<PathNode> findNeighbors(PathNode node, byte[,] roomGrid)
    {
        var neighbors = new List<PathNode>();
        int row = node.point.i;
        int col = node.point.j;

        bool hasUp, hasDown, hasLeft, hasRight;
        hasUp = hasDown = hasLeft = hasRight = false;

        if (row > 0 && roomGrid[row - 1, col] > 0)
        {
            neighbors.Add(new PathNode(new RoomPoint(row - 1, col)));
            hasUp = true;
        }
        if (row < Room.roomTileHeightCount - 1 && roomGrid[row + 1, col] > 0)
        {
            neighbors.Add(new PathNode(new RoomPoint(row + 1, col)));
            hasDown = true;
        }
        if (col > 0 && roomGrid[row, col - 1] > 0)
        {
            neighbors.Add(new PathNode(new RoomPoint(row, col - 1)));
            hasLeft = true;
        }
        if (col < Room.roomTileWidthCount - 1 && roomGrid[row, col + 1] > 0)
        {
            neighbors.Add(new PathNode(new RoomPoint(row, col + 1)));
            hasRight = true;
        }

        if (hasUp && hasLeft && roomGrid[row - 1, col - 1] > 0)
        {
            neighbors.Add(new PathNode(new RoomPoint(row - 1, col - 1)));
        }
        if (hasUp && hasRight && roomGrid[row - 1, col + 1] > 0)
        {
            neighbors.Add(new PathNode(new RoomPoint(row - 1, col + 1)));
        }
        if (hasDown && hasLeft && roomGrid[row + 1, col - 1] > 0)
        {
            neighbors.Add(new PathNode(new RoomPoint(row + 1, col - 1)));
        }
        if (hasDown && hasRight && roomGrid[row + 1, col + 1] > 0)
        {
            neighbors.Add(new PathNode(new RoomPoint(row + 1, col + 1)));
        }

        return neighbors;
    }

    // If actor can move directly from start to end ignoring room grid and not collide with obstacles
    private static bool CanTravelDirectly(RoomPoint start, RoomPoint end, byte[,] roomGrid)
    {
        return false;
    }
}
