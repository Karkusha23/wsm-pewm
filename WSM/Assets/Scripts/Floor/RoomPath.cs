using System.Collections.Generic;
using UnityEngine;

public static class RoomPath
{
    // Size of actor box collider divided by tile size
    public const float actorSize = 0.8f;

    // Maximum actor offset relative to room grid with which it still can fit through narrow paths
    public const float maxAllowedActorGridOffset = Room.tileSize * (1.0f - RoomPath.actorSize) / 2.0f;

    // Stores coordinates in room grid
    public class RoomPoint
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

        public override string ToString()
        {
            return string.Format("RoomPoint({0}, {1})", i, j);
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
            /*string str = "";
            for (int i = 0; i < roomGrid.GetLength(0); ++i)
            {
                for (int j = 0; j < roomGrid.GetLength(1); ++j)
                {
                    var point = new PathNode(new RoomPoint(i, j));
                    if (openList.Contains(point))
                    {
                        str += '2';
                    }
                    else if (closedSet.Contains(point))
                    {
                        str += '3';
                    }
                    else
                    {
                        str += roomGrid[i, j];
                    }
                }
                str += '\n';
            }
            Debug.Log(str);*/

            var curNode = openList.Min;
            openList.Remove(curNode);

            if (curNode == endNode)
            {
                return makePathFromNode(curNode);
            }

            closedSet.Add(curNode);

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
        for (int i = 0; i < path.Count - 2; ++i)
        {
            while (i < path.Count - 2 && Raycast(path[i], path[i + 2], roomGrid, true))
            {
                path.RemoveAt(i + 1);
            }
        }
        return path;
    }

    // Build smoothed path using local coordinates of start and end points
    public static Path BuildPathSmoothed(Vector3 start, Vector3 end, byte[,] roomGrid)
    {
        return BuildPathSmoothed(Room.LocalToRoomPoint(start), Room.LocalToRoomPoint(end), roomGrid);
    }

    // Returns true if there's no obstacles between start and end
    // If considerTravelCost is true, points with travel cost greater than maximum of start and end points travel cost will be counted as untravable
    public static bool Raycast(RoomPoint start, RoomPoint end, byte[,] roomGrid, bool considerTravelCost)
    {
        if (start == end)
        {
            return true;
        }

        int maxTravelCost = considerTravelCost ? Mathf.Max(roomGrid[start.i, start.j], roomGrid[end.i, end.j]) : 0;

        if (start.i == end.i)
        {
            if (start.j > end.j)
            {
                RoomPoint tmp = start;
                start = end;
                end = tmp;
            }
            for (int j = start.j; j <= end.j; ++j)
            {
                if (!IsPointTravable(start.i, j, roomGrid, maxTravelCost))
                {
                    return false;
                }
            }
            return true;
        }

        if (start.j == end.j)
        {
            if (start.i > end.i)
            {
                RoomPoint tmp = start;
                start = end;
                end = tmp;
            }
            for (int i = start.i; i <= end.i; ++i)
            {
                if (!IsPointTravable(i, start.j, roomGrid, maxTravelCost))
                {
                    return false;
                }
            }
            return true;
        }

        if (Mathf.Abs(end.i - start.i) == Mathf.Abs(end.j - start.j))
        {
            int rowDir = start.i < end.i ? 1 : -1;
            int colDir = start.j < end.j ? 1 : -1;
            int row = start.i;
            int col = start.j;
            while (row != end.i)
            {
                if (!IsPointTravable(row, col, roomGrid, maxTravelCost) || !IsPointTravable(row + rowDir, col, roomGrid, maxTravelCost) ||
                    !IsPointTravable(row, col + colDir, roomGrid, maxTravelCost))
                {
                    return false;
                }
                row += rowDir;
                col += colDir;
            }
            return IsPointTravable(end.i, end.j, roomGrid, maxTravelCost);
        }

        if (Mathf.Abs(end.i - start.i) < Mathf.Abs(end.j - start.j))
        {
            if (start.j > end.j)
            {
                RoomPoint tmp = start;
                start = end;
                end = tmp;
            }
            float halfRowDirection = (float)(end.i - start.i) / ((float)(end.j - start.j) * 2.0f);
            float row = (float)start.i;
            for (float j = start.j; j < end.j; j += 0.5f)
            {
                if (!IsNeighborhoodTravable(row, j, roomGrid, maxTravelCost))
                {
                    return false;
                }
                row += halfRowDirection;
            }
            if (!IsNeighborhoodTravable(row, end.j, roomGrid, maxTravelCost))
            {
                return false;
            }
            return true;
        }

        if (start.i > end.i)
        {
            RoomPoint tmp = start;
            start = end;
            end = tmp;
        }
        float halfColDirection = (float)(end.j - start.j) / ((float)(end.i - start.i) * 2.0f);
        float column = (float)start.j;
        for (float i = start.i; i < end.i; i += 0.5f)
        {
            int colInt = Mathf.RoundToInt(column);
            if (!IsNeighborhoodTravable(i, column, roomGrid, maxTravelCost))
            {
                return false;
            }
            column += halfColDirection;
        }
        if (!IsNeighborhoodTravable(end.i, column, roomGrid, maxTravelCost))
        {
            return false;
        }
        return true;
    }

    // Returns true, if there's no obstacles between start and end points. This overload of raycast takes into account fractional parts of local coordinates start and end points 
    // If considerTravelCost is true, points with travel cost greater than maximum of start and end points travel cost will be counted as untravable
    public static bool Raycast(Vector3 start, Vector3 end, byte[,] roomGrid, bool considerTravelCost)
    {
        float distance = Vector3.Distance(start, end);

        if (distance <= Room.tileSize)
        {
            return true;
        }

        RoomPoint startPoint = Room.LocalToRoomPoint(start);
        RoomPoint endPoint = Room.LocalToRoomPoint(end);

        int maxTravelCost = considerTravelCost ? Mathf.Max(roomGrid[startPoint.i, startPoint.j], roomGrid[endPoint.i, endPoint.j]) : 0;

        int checksCount = Mathf.Max(3, Mathf.CeilToInt(distance * 2.0f / Room.tileSize));

        Vector2 startVec = Room.LocalToFractionalRoomPoint(start);
        Vector2 endVec = Room.LocalToFractionalRoomPoint(end);

        float row = startVec.x;
        float col = startVec.y;

        float rowDir = (endVec.x - startVec.x) / (float)checksCount;
        float colDir = (endVec.y - startVec.y) / (float)checksCount;

        for (int i = 0; i < checksCount; ++i)
        {
            if (!IsNeighborhoodTravable(row, col, roomGrid, maxTravelCost))
            {
                return false;
            }
            row += rowDir;
            col += colDir;
        }
        return true;
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
            fullPath = -1;
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
                if (node1.point == node2.point)
                {
                    return 0;
                }
                if (node1.fullPath < node2.fullPath)
                {
                    return -1;
                }
                if (node1.fullPath > node2.fullPath)
                {
                    return 1;
                }
                if (node1.GetHashCode() < node2.GetHashCode())
                {
                    return -1;
                }
                if (node1.GetHashCode() > node2.GetHashCode())
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
        if (row < roomGrid.GetLength(0) - 1 && roomGrid[row + 1, col] > 0)
        {
            neighbors.Add(new PathNode(new RoomPoint(row + 1, col)));
            hasDown = true;
        }
        if (col > 0 && roomGrid[row, col - 1] > 0)
        {
            neighbors.Add(new PathNode(new RoomPoint(row, col - 1)));
            hasLeft = true;
        }
        if (col < roomGrid.GetLength(1) - 1 && roomGrid[row, col + 1] > 0)
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

    private static bool IsNeighborhoodTravable(float row, float col, byte[,] roomGrid, int maxTravelCost)
    {
        var rows = new List<int>();
        var cols = new List<int>();

        if (Mathf.Abs(row - Mathf.Round(row)) > actorSize / 2.0f)
        {
            rows.Add(Mathf.RoundToInt(row));
        }
        else
        {
            rows.Add(Mathf.FloorToInt(row));
            rows.Add(Mathf.CeilToInt(row));
        }

        if (Mathf.Abs(col - Mathf.Round(col)) > actorSize / 2.0f)
        {
            cols.Add(Mathf.RoundToInt(col));
        }
        else
        {
            cols.Add(Mathf.FloorToInt(col));
            cols.Add(Mathf.CeilToInt(col));
        }

        foreach (var i in rows)
        {
            foreach (var j in cols)
            {
                if (!IsPointTravable(i, j, roomGrid, maxTravelCost))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsPointTravable(int row, int col, byte[,] roomGrid, int maxTravelCost)
    {
        return row > 0 && col > 0 && row < roomGrid.GetLength(0) && col < roomGrid.GetLength(1) && roomGrid[row, col] > 0 && (maxTravelCost == 0 || !(roomGrid[row, col] > maxTravelCost));
    }
}