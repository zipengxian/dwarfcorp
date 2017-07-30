// WaterManager.cs
// 
//  Modified MIT License (MIT)
//  
//  Copyright (c) 2015 Completely Fair Games Ltd.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// The following content pieces are considered PROPRIETARY and may not be used
// in any derivative works, commercial or non commercial, without explicit 
// written permission from Completely Fair Games:
// 
// * Images (sprites, textures, etc.)
// * 3D Models
// * Sound Effects
// * Music
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Threading;

namespace DwarfCorp
{
    public enum LiquidType
    {
        None = 0,
        Water,
        Lava,
        Count
    }

    /// <summary>
    /// Handles the water simulation in the game.
    /// </summary>
    public class WaterManager
    {
        private ChunkManager Chunks { get; set; }
        public byte EvaporationLevel { get; set; }

        public static byte maxWaterLevel = 8;
        public static byte threeQuarterWaterLevel = 6;
        public static byte oneHalfWaterLevel = 4;
        public static byte oneQuarterWaterLevel = 2;
        public static byte rainFallAmount = 1;
        public static byte inWaterThreshold = 5;
        public static byte waterMoveThreshold = 1;

        private int[][] SlicePermutations;
        private int[][] NeighborPermutations = new int[][]
        {
            new int[] { 0, 1, 2, 3 },
            new int[] { 0, 1, 3, 2 },
            new int[] { 0, 2, 1, 3 },
            new int[] { 0, 2, 3, 1 },
            new int[] { 0, 3, 1, 2 },
            new int[] { 0, 3, 2, 1 }
        };

        private int[] NeighborScratch = new int[4];

        private void RollArray(int[] from, int[] into, int offset)
        {
            for (var i = 0; i < 4; ++i)
            {
                into[offset] = from[i];
                offset = (offset + 1) & 0x3;
            }
        }
                
        private LinkedList<LiquidSplash> Splashes = new LinkedList<LiquidSplash>();
        private Mutex SplashLock = new Mutex();
        private LinkedList<LiquidTransfer> Transfers = new LinkedList<LiquidTransfer>();
        private Mutex TransferLock = new Mutex();

        public IEnumerable<LiquidSplash> GetSplashQueue()
        {
            SplashLock.WaitOne();
            var r = Splashes;
            Splashes = new LinkedList<LiquidSplash>();
            SplashLock.ReleaseMutex();
            return r;
        }

        // Todo: Delete after verifying that creating stone on the water thread is okay.
        public IEnumerable<LiquidTransfer> GetTransferQueue()
        {
            TransferLock.WaitOne();
            var r = Transfers;
            Transfers = new LinkedList<LiquidTransfer>();
            TransferLock.ReleaseMutex();
            return r;
        }

        public WaterManager(ChunkManager chunks)
        {
            Chunks = chunks;
            EvaporationLevel = 1;
            ChunkData data = chunks.ChunkData;

            // Create permutation arrays for random update orders.
            SlicePermutations = new int[16][];
            var temp = new int[VoxelConstants.ChunkSizeX * VoxelConstants.ChunkSizeZ];
            for (var i = 0; i < temp.Length; ++i)
                temp[i] = i;
            for (var i = 0; i < 16; ++i)
            {
                temp.Shuffle();
                SlicePermutations[i] = temp.ToArray(); // Copies the array
            }
        }

        public void CreateTransfer(TemporaryVoxelHandle Vox, WaterCell From, WaterCell To)
        {
            if ((From.Type == LiquidType.Lava && To.Type == LiquidType.Water)
                || (From.Type == LiquidType.Water && To.Type == LiquidType.Lava))
            {
                Vox.Type = VoxelLibrary.GetVoxelType("Stone");
                Vox.WaterCell = WaterCell.Empty;
                Vox.Chunk.ShouldRebuild = true;
                Vox.Chunk.ShouldRecalculateLighting = true;
            }            
        }

        public void CreateSplash(Vector3 pos, LiquidType liquid)
        {
            if (MathFunctions.RandEvent(0.9f)) return;

            LiquidSplash splash;

            switch(liquid)
            {
                case LiquidType.Water:
                {
                    splash = new LiquidSplash
                    {
                        name = "splash2",
                        numSplashes = 2,
                        position = pos,
                        sound = ContentPaths.Audio.river
                    };
                }
                    break;
                case LiquidType.Lava:
                {
                    splash = new LiquidSplash
                    {
                        name = "flame",
                        numSplashes = 5,
                        position = pos,
                        sound = ContentPaths.Audio.fire
                    };
                }
                    break;
                default:
                    throw new InvalidOperationException();
            }

            SplashLock.WaitOne();
            Splashes.AddFirst(splash);
            SplashLock.ReleaseMutex();
        }

        public float GetSpreadRate(LiquidType type)
        {
            switch (type)
            {
                case LiquidType.Lava:
                    return 0.1f + MathFunctions.Rand() * 0.1f;
                case LiquidType.Water:
                    return 0.5f;
            }

            return 1.0f;
        }

        public void UpdateWater()
        {
            //Chunks.camera.Position;


            if(Chunks.World.Paused)
            {
                return;
            }

            foreach(var chunk in Chunks.ChunkData.GetChunkEnumerator())
            {
                bool didUpdate = DiscreteUpdate(chunk);

                if (!didUpdate && !chunk.FirstWaterIter)
                {
                    chunk.FirstWaterIter = false;
                    continue;
                }

                chunk.ShouldRebuildWater = true;
                chunk.FirstWaterIter = false;
            }
        }

        private bool DiscreteUpdate(VoxelChunk chunk)
        {
            bool updateOccured = false;

            for (var y = 0; y < VoxelConstants.ChunkSizeY; ++y)
            {
                // Apply 'liquid present' tracking in voxel data to skip entire slices.
                if (chunk.Data.LiquidPresent[y] == 0) continue;

                var layerOrder = SlicePermutations[MathFunctions.RandInt(0, SlicePermutations.Length)];

                for (var i = 0; i < layerOrder.Length; ++i)
                {
                    var x = layerOrder[i] % VoxelConstants.ChunkSizeX;
                    var z = (layerOrder[i] >> VoxelConstants.XDivShift) % VoxelConstants.ChunkSizeZ;
                    var currentVoxel = new TemporaryVoxelHandle(chunk, new LocalVoxelCoordinate(x, y, z));

                    if (currentVoxel.TypeID != 0)
                        continue;

                    var water = currentVoxel.WaterCell;

                    if (water.WaterLevel < 1 || water.Type == LiquidType.None)
                        continue;

                    // Evaporate.
                    //if (water.WaterLevel <= EvaporationLevel && MathFunctions.RandEvent(0.01f))
                    //{
                    //    if (water.Type == LiquidType.Lava)
                    //    {
                    //        currentVoxel.Type = VoxelLibrary.GetVoxelType("Stone");
                    //        chunk.ShouldRebuild = true;
                    //        chunk.ShouldRecalculateLighting = true;
                    //    }

                    //    currentVoxel.WaterCell = new WaterCell
                    //    {
                    //        Type = LiquidType.None,
                    //        WaterLevel = 0
                    //    };

                    //    updateOccured = true;
                    //    continue;
                    //}

                    var voxBelow = (y > 0) ? new TemporaryVoxelHandle(chunk, new LocalVoxelCoordinate(x, y - 1, z)) : TemporaryVoxelHandle.InvalidHandle;

                    if (voxBelow.IsValid && voxBelow.IsEmpty)
                    {
                        // Fall into the voxel below.

                        var belowWater = voxBelow.WaterCell;

                        // Special case: No liquid below, just drop down.
                        if (belowWater.WaterLevel == 0)
                        {
                            CreateSplash(currentVoxel.Coordinate.ToVector3(), water.Type);
                            voxBelow.WaterCell = water;
                            currentVoxel.WaterCell = WaterCell.Empty;
                            CreateTransfer(voxBelow, water, belowWater);
                            updateOccured = true;
                            continue;
                        }

                        var spaceLeftBelow = maxWaterLevel - belowWater.WaterLevel;

                        if (spaceLeftBelow >= water.WaterLevel)
                        {
                            CreateSplash(currentVoxel.Coordinate.ToVector3(), water.Type);
                            belowWater.WaterLevel += water.WaterLevel;
                            voxBelow.WaterCell = belowWater;
                            currentVoxel.WaterCell = WaterCell.Empty;
                            CreateTransfer(voxBelow, water, belowWater);
                            updateOccured = true;
                            continue;
                        }

                        if (spaceLeftBelow > 0)
                        {
                            CreateSplash(currentVoxel.Coordinate.ToVector3(), water.Type);
                            water.WaterLevel = (byte)(water.WaterLevel - maxWaterLevel + belowWater.WaterLevel);
                            belowWater.WaterLevel = maxWaterLevel;
                            voxBelow.WaterCell = belowWater;
                            currentVoxel.WaterCell = water;
                            CreateTransfer(voxBelow, water, belowWater);
                            updateOccured = true;
                            continue;
                        }
                    }

                    if (water.WaterLevel <= 1) continue;

                    // Nothing left to do but spread.

                    RollArray(NeighborPermutations[MathFunctions.RandInt(0, NeighborPermutations.Length)], NeighborScratch, MathFunctions.RandInt(0, 4));

                    for (var n = 0; n < NeighborScratch.Length; ++n)
                    {
                        var neighborOffset = VoxelHelpers.ManhattanNeighbors2D[NeighborScratch[n]];
                        var neighborVoxel = new TemporaryVoxelHandle(Chunks.ChunkData,
                            currentVoxel.Coordinate + neighborOffset);

                        if (neighborVoxel.IsValid && neighborVoxel.IsEmpty)
                        {
                            var neighborWater = neighborVoxel.WaterCell;

                            if (neighborWater.WaterLevel < water.WaterLevel)
                            {
                                var amountToMove = (int)(water.WaterLevel * GetSpreadRate(water.Type));
                                if (neighborWater.WaterLevel + amountToMove > maxWaterLevel)
                                    amountToMove = maxWaterLevel - neighborWater.WaterLevel;
                                var newWater = water.WaterLevel - amountToMove;

                                currentVoxel.WaterCell = new WaterCell
                                {
                                    Type = newWater == 0 ? LiquidType.None : water.Type,
                                    WaterLevel = (byte)(water.WaterLevel - amountToMove)
                                };

                                neighborVoxel.WaterCell = new WaterCell
                                {
                                    Type = neighborWater.Type == LiquidType.None ? water.Type : neighborWater.Type,
                                    WaterLevel = (byte)(neighborWater.WaterLevel + amountToMove)
                                };

                                CreateTransfer(neighborVoxel, water, neighborWater);
                                updateOccured = true;
                                break; 
                            }

                        }
                    }
                }
            }

            return updateOccured;
        }

    }
}
