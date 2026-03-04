using System.Collections.Generic;

namespace Retrograde.Models;

public class Tile
{
    public List<string> prefabs = new List<string>();
    public int rotation = 0;
    public float zoverride = 0;
}

public class GenerationMap
{
    public Tile[][] tiles;
    public int xsize;
    public int ysize;

    public GenerationMap(int x, int y)
    {
        xsize = x;
        ysize = y;

        tiles = new Tile[x][];
        for (int i = 0; i < xsize; i++)
        {
            tiles[i] = new Tile[ysize];
            for (int j = 0; j < ysize; j++)
            {
                tiles[i][j] = new Tile();
            }
        }
    }

    public void replacetile(int x, int y, string type, int rotation)
    {
        if (!tiles[x][y].prefabs.Contains(type))
        {
            tiles[x][y].prefabs.Clear();
            tiles[x][y].rotation = rotation;
            tiles[x][y].prefabs.Add(type);
        }
    }

    public void addontile(int x, int y, string type, int rotation)
    {
        if (!tiles[x][y].prefabs.Contains(type))
        {
            tiles[x][y].prefabs.Add(type);
        }
    }

    public bool canPlace(int x, int y)
    {
        if (x > xsize - 3 || x < 1 || y > ysize - 3 || y < 1)
        {
            return false;
        }
        if (tiles[x][y].prefabs.Count > 0) return false;
        if (tiles[x][y + 1].prefabs.Count > 0) return false;
        if (tiles[x][y - 1].prefabs.Count > 0) return false;

        if (tiles[x + 1][y].prefabs.Count > 0) return false;
        if (tiles[x + 1][y + 1].prefabs.Count > 0) return false;
        if (tiles[x + 1][y - 1].prefabs.Count > 0) return false;

        if (tiles[x - 1][y].prefabs.Count > 0) return false;
        if (tiles[x - 1][y + 1].prefabs.Count > 0) return false;
        if (tiles[x - 1][y - 1].prefabs.Count > 0) return false;

        return true;
    }

    public bool placesmalltileonempty(int x, int y, string type, int rotation, string filltag)
    {
        if (canPlace(x, y))
        {
            return placesmalltile(x, y, type, rotation, filltag);
        }
        return false;
    }

    public bool placesingletile(int x, int y, string type, int rotation)
    {
        if (x > xsize || x < 0 || y > ysize || y < 0)
        {
            return false;
        }
        replacetile(x, y, type, rotation);
        return true;
    }

    public bool placelargetile(int x, int y, string type, int rotation, string filltag)
    {
        if (x > xsize - 6 || x < 6 || y > ysize - 6 || y < 6)
        {
            return false;
        }

        for (int i = 0; i < 9; i++)
        {
            for (int j = 0; j < 9; j++)
            {
                replacetile((x - 4) + i, (y - 4) + j, filltag, 0);
            }
        }
        replacetile(x, y, type, rotation);
        return true;
    }

    public bool placelandingpadtile(int x, int y, string type, int rotation, string filltag)
    {
        if (x > xsize - 6 || x < 6 || y > ysize - 6 || y < 6)
        {
            return false;
        }

        for (int i = 0; i < 9; i++)
        {
            for (int j = 0; j < 9; j++)
            {
                replacetile((x - 4) + i, (y - 4) + j, filltag, 0);
            }
        }
        replacetile(x, y, type, rotation);
        tiles[x][y].zoverride = -13.1000f;

        return true;
    }

    public void placeSquareofsmalltiles(int size, int centerx, int centery, string type, string filltag)
    {
        for (int x = centerx - (size / 3); x <= centerx + (size / 3); x += 3)
        {
            for (int y = centery - (size / 3); y <= centery + (size / 3); y += 3)
            {
                placesmalltileonempty(x, y, type, 0, "floor");
            }
        }
    }

    public bool placesmalltile(int x, int y, string type, int rotation, string filltag)
    {
        if (x > xsize - 3 || x < 1 || y > ysize - 3 || y < 1)
        {
            return false;
        }
        //Col 1
        replacetile(x - 1, y, filltag, 0);
        replacetile(x - 1, y - 1, filltag, 0);
        replacetile(x - 1, y + 1, filltag, 0);
        //Col 2
        replacetile(x, y, type, rotation);
        replacetile(x, y - 1, filltag, 0);
        replacetile(x, y + 1, filltag, 0);
        //Col 3
        replacetile(x + 1, y, filltag, 0);
        replacetile(x + 1, y - 1, filltag, 0);
        replacetile(x + 1, y + 1, filltag, 0);
        return true;
    }

    public bool placesmalladdontile(int x, int y, string type, int rotation, string filltag)
    {
        addontile(x, y, type, rotation);
        return true;
    }
}
