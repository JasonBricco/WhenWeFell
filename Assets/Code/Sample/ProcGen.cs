﻿//using System.Collections;
//using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class ProcGen
{
    private void goDown(int[,] level, ref int roomX, ref int roomY, ref int prev)
    {
        //if trying to go down out of the level, the path is complete
        if (roomY == level.GetLength(1) - 1) {
            //if the previous movement was down, make the current room type 3.
            if (prev == 2 || prev == 4) {
                level[roomX, roomY] = 3;
            }
            roomY++;
            return;
        }

        if (prev == 2 || prev == 4) {
            level[roomX, roomY] = 4;
            prev = 4;
        } else {
            level[roomX, roomY] = 2;
            prev = 2;
        }
        roomY++;
    }

    private void goLeft(int[,] level, ref int roomX, ref int roomY, ref int prev)
    {
        if (roomX == 0) {
            level[roomX, roomY] = 1;
            goDown(level, ref roomX, ref roomY, ref prev);
            return;
        }

        if (prev == 2 || prev == 4) {
            level[roomX, roomY] = 3;
            prev = 3;
        } else if (level[roomX, roomY] == 3) {
            //3 type rooms already have side exits
            //do nothing so the top exit gets preserved    
        } else {
            level[roomX, roomY] = 1;
            prev = 1;
        }
        roomX--;
    }

    private void goRight(int[,] level, ref int roomX, ref int roomY, ref int prev)
    {
        if (roomX == level.GetLength(0) - 1) {
            level[roomX, roomY] = 1;
            goDown(level, ref roomX, ref roomY, ref prev);
            return;
        }

        if (prev == 2 || prev == 4) {
            level[roomX, roomY] = 3;
            prev = 3;
        } else if (level[roomX, roomY] == 3) {
            //3 type rooms already have side exits
            //do nothing so the top exit gets preserved    
        } else {
            level[roomX, roomY] = 1;
            prev = 1;
        }
        roomX++;
    }

    private void makeSolutionPath(int[,] level)
    {
        int roomX = Random.Range(0, 4);
        int roomY = 0;

        int direction = 0;
        int prevRoom = 0;
        level[roomX, roomY] = 1;

        while (roomY < level.GetLength(1))
        {
            direction = Random.Range(0,5);
            Debug.Log(direction);

            if (direction == 0 || direction == 1)
            {
                //go left
                goLeft(level, ref roomX, ref roomY, ref prevRoom);

            } else if (direction == 2 || direction == 3)
            {
                //go right
                goRight(level, ref roomX, ref roomY, ref prevRoom);

            } else
            {
                //go down
                goDown(level, ref roomX, ref roomY, ref prevRoom);
            }
        }
    }

    public void Generate(World world)
    {
        //2d array of rooms
        int[,] level = new int[4,4];
        makeSolutionPath(level);

        for (int y = 0; y < level.GetLength(1); y++) {
            string output = "";
            for (int x = 0; x < level.GetLength(0); x++) {
                output += level[x, y];
            }
            Debug.Log(output);
        }

        TextAsset[,] rooms = new TextAsset[5,1];

        for (int i = 0; i < rooms.GetLength(0); i++) {
            TextAsset[] temp = Resources.LoadAll<TextAsset>("RoomData/type" + i);
            for (int j = 0; j < rooms.GetLength(1); j++) {
                rooms[i, j] = temp[j];
            }
        }

        int type;
        int room;
        int row = 3;
        Chunk chunk;
        for (int y = 0; y < level.GetLength(0); y++) {
            for (int x = 0; x < level.GetLength(1); x++) {
                type = level[x, y];
                room = Random.Range(0, rooms.GetLength(1));

                chunk = new Chunk(x, row, rooms[type, room].text);
                world.SetChunk(x, row, chunk);
            }
            row--;
        }
        
    }

    
}
