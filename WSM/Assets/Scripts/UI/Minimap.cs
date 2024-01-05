using UnityEngine;

public class Minimap : MonoBehaviour
{
    public int pixHalfARoom = 10;
    public int minimapHeight = 10;
    public int minimapWidth = 10;

    public GameObject smallRoomNotExploredPrefab;
    public GameObject smallRoomExploredPrefab;
    public GameObject bigRoomNotExploredPrefab;
    public GameObject bigRoomExploredPrefab;
    public GameObject bossIconPrefab;

    [HideInInspector]
    public byte[,] floorMatrix;
    [HideInInspector]
    public int floorHeight;
    [HideInInspector]
    public int floorWidth;

    private byte[,] floorExplored;
    // 0 - not explored
    // 1 - explored small room, not visited
    // 2 - explored big room core, not visited
    // 3 - explored big room units, not visited
    // 4 - small room visited
    // 5 - big room core visited
    // 6 - big room subuints visited
    private int curRow;
    private int curCol;
    private Transform minimapBase;
    private GameObject[,] notExploredRooms;
    private GameObject[,] exploredRooms;
    private int pixARoom;
    private Vector3 camOffset;

    private void Start()
    {
        var floor = GameObject.FindGameObjectWithTag("Floor").GetComponent<Floor>();

        floorMatrix = floor.FloorMatrix;
        floorHeight = floor.floorHeight;
        floorWidth = floor.floorWidth;

        camOffset = Camera.main.ScreenToWorldPoint(Vector3.zero);
        curRow = floorHeight / 2;
        curCol = floorWidth / 2;
        pixARoom = pixHalfARoom * 2;
        floorExplored = new byte[floorHeight, floorWidth];
        minimapBase = transform.Find("MinimapBase");
        notExploredRooms = new GameObject[floorHeight, floorWidth];
        exploredRooms = new GameObject[floorHeight, floorWidth];
        for (int i = 0; i < floorHeight; ++i)
        {
            for (int j = 0; j < floorWidth; ++j)
            {
                if (floorMatrix[i, j] == 4 || floorMatrix[i, j] == 8 || floorMatrix[i, j] == 9)
                {
                    Vector2 newSmallRoomOffset = new Vector2((j - floorWidth / 2) * pixARoom, (floorHeight / 2 - i) * pixARoom);

                    notExploredRooms[i, j] = Instantiate(smallRoomNotExploredPrefab, minimapBase);
                    notExploredRooms[i, j].SetActive(false);
                    notExploredRooms[i, j].GetComponent<RectTransform>().anchoredPosition += newSmallRoomOffset;

                    exploredRooms[i, j] = Instantiate(smallRoomExploredPrefab, minimapBase);
                    exploredRooms[i, j].SetActive(false);
                    exploredRooms[i, j].GetComponent<RectTransform>().anchoredPosition += newSmallRoomOffset;
                }
                else if (floorMatrix[i, j] == 5 || floorMatrix[i, j] == 7)
                {
                    Vector2 newBigRoomOffset = new Vector2((j - floorWidth / 2) * pixARoom + pixHalfARoom, (floorHeight / 2 - i) * pixARoom - pixHalfARoom);

                    notExploredRooms[i, j] = notExploredRooms[i, j + 1] = notExploredRooms[i + 1, j] = notExploredRooms[i + 1, j + 1] = Instantiate(bigRoomNotExploredPrefab, minimapBase);
                    exploredRooms[i, j] = exploredRooms[i, j + 1] = exploredRooms[i + 1, j] = exploredRooms[i + 1, j + 1] = Instantiate(bigRoomExploredPrefab, minimapBase);

                    if (floorMatrix[i, j] == 7)
                    {
                        Instantiate(bossIconPrefab, notExploredRooms[i, j].transform.position, Quaternion.identity, notExploredRooms[i, j].transform);
                        Instantiate(bossIconPrefab, exploredRooms[i, j].transform.position, Quaternion.identity, exploredRooms[i, j].transform);
                    }

                    notExploredRooms[i, j].GetComponent<RectTransform>().anchoredPosition += newBigRoomOffset;
                    exploredRooms[i, j].GetComponent<RectTransform>().anchoredPosition += newBigRoomOffset;

                    notExploredRooms[i, j].SetActive(false);
                    exploredRooms[i, j].SetActive(false);
                }

            }
        }
    }

    public void checkRoom(Vector3 roomPos)
    {
        int row = floorHeight / 2 - Mathf.RoundToInt(roomPos.y / Floor.roomHeight);
        int col = floorWidth / 2 + Mathf.RoundToInt(roomPos.x / Floor.roomWidth);
        if (floorExplored[row, col] < 4)
        {
            if (floorMatrix[row, col] == 4 || floorMatrix[row, col] == 8 || floorMatrix[row, col] == 9)
            {
                floorExplored[row, col] = 4;
            }
            else if (floorMatrix[row, col] == 5 || floorMatrix[row, col] == 7)
            {
                floorExplored[row, col] = 5;
                floorExplored[row, col + 1] = floorExplored[row + 1, col] = floorExplored[row + 1, col + 1] = 6;
            }
            exploredRooms[row, col].SetActive(true);
            notExploredRooms[row, col].SetActive(false);
            if (floorMatrix[row, col] == 4 || floorMatrix[row, col] == 8 || floorMatrix[row, col] == 9)
            {
                checkSmallAround(row, col);
            }
            else if (floorMatrix[row, col] == 5)
            {
                checkBigAround(row, col);
            }
        }
        minimapBase.GetComponent<RectTransform>().anchoredPosition += new Vector2((curCol - col) * pixARoom, (row - curRow) * pixARoom);
        curRow = row;
        curCol = col;
        checkToActivate();
    }

    private void checkSmallAround(int row, int col)
    {
        checkCell(row - 1, col);
        checkCell(row, col - 1);
        checkCell(row, col + 1);
        checkCell(row + 1, col);
    }

    private void checkBigAround(int row, int col)
    {
        checkCell(row - 1, col);
        checkCell(row - 1, col + 1);
        checkCell(row, col - 1);
        checkCell(row, col + 2);
        checkCell(row + 1, col - 1);
        checkCell(row + 1, col + 2);
        checkCell(row + 2, col);
        checkCell(row + 2, col + 1);
    }

    private void checkCell(int row, int col)
    {
        if (floorExplored[row, col] == 0 && floorMatrix[row, col] >= 4)
        {
            notExploredRooms[row, col].SetActive(true);
            if (floorMatrix[row, col] == 4 || floorMatrix[row, col] == 8)
            {
                floorExplored[row, col] = 1;
            }
            else if (floorMatrix[row, col] == 5 || floorMatrix[row, col] == 7)
            {
                floorExplored[row, col] = 2;
                floorExplored[row, col + 1] = floorExplored[row + 1, col] = floorExplored[row + 1, col + 1] = 3;
            }
            else if (floorMatrix[row, col] == 6)
            {
                if (floorMatrix[row, col - 1] == 5 || floorMatrix[row, col - 1] == 7)
                {
                    floorExplored[row, col - 1] = 2;
                    floorExplored[row, col] = floorExplored[row + 1, col - 1] = floorExplored[row + 1, col] = 3;
                }
                else if (floorMatrix[row - 1, col] == 5 || floorMatrix[row - 1, col] == 7)
                {
                    floorExplored[row - 1, col] = 2;
                    floorExplored[row - 1, col + 1] = floorExplored[row, col] = floorExplored[row, col + 1] = 3;
                }
                else
                {
                    floorExplored[row - 1, col - 1] = 2;
                    floorExplored[row - 1, col] = floorExplored[row, col - 1] = floorExplored[row, col] = 3;
                }
            }
        }
    }

    private void checkToActivate()
    {
        for (int i = 0; i < floorHeight; ++i)
        {
            for (int j = 0; j < floorWidth; ++j)
            {
                if (floorExplored[i, j] > 0)
                {
                    if (Mathf.Abs(i - curRow) <= minimapHeight / 2 - 1 && Mathf.Abs(j - curCol) <= minimapWidth / 2 - 1)
                    {
                        if (floorExplored[i, j] >= 1 && floorExplored[i, j] <= 3)
                        {
                            notExploredRooms[i, j].SetActive(true);
                        }
                        else if (floorExplored[i, j] >= 4 && floorExplored[i, j] <= 6)
                        {
                            exploredRooms[i, j].SetActive(true);
                        }
                    }
                    else
                    {
                        notExploredRooms[i, j].SetActive(false);
                        exploredRooms[i, j].SetActive(false);
                    }
                }
            }
        }
    }
}