﻿namespace RGBDReconstruction.Strategies;

public class VoxelCube
{
    public static readonly int[][] Vertices = {
        [0, 0, 0],
        [0, 0, 1],
        [1, 0, 1],
        [1, 0, 0],
        [0, 1, 0],
        [0, 1, 1],
        [1, 1, 1],
        [1, 1, 0]
    };

    public static readonly int[][] Edges =
    {
        [ 0, 1 ],
        [ 1, 2 ],
        [ 2, 3 ],
        [ 3, 0 ],
        [ 4, 5 ],
        [ 5, 6 ],
        [ 6, 7 ],
        [ 7, 4 ],
        [ 0, 4 ],
        [ 1, 5 ],
        [ 2, 6 ],
        [ 3, 7 ],
    };
};