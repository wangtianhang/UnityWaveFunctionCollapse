﻿/*
The MIT License(MIT)
Copyright(c) mxgmn 2016.
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
The software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WFC
{

    class OverlappingModel : Model
    {
        int N;
        byte[][] patterns;
        List<Color> colors;
        int ground;

        public OverlappingModel(string name, int N, int width, int height, bool periodicInput, bool periodicOutput, int symmetry, int ground)
            : base(width, height)
        {
            this.N = N;
            periodic = periodicOutput;

            string resPath = $"samples/{name}";
            Texture2D texture = Resources.Load<Texture2D>(resPath);
            int SMX = texture.width;
            int SMY = texture.height;
            byte[,] sample = new byte[SMX, SMY];
            colors = new List<Color>();

            for (int y = 0; y < SMY; y++) for (int x = 0; x < SMX; x++)
                {
                    Color color = texture.GetPixel(x, y);

                    int i = 0;
                    foreach (var c in colors)
                    {
                        if (c == color)//为减少colors长度的操作
                        {
                            break;
                        }
                        i++;
                    }

                    if (i == colors.Count)//i==colors.Count证明遍历完colors还没有color，所以add
                    {
                        colors.Add(color);
                    }
                    sample[x, y] = (byte)i;//此xy记录color在colors位置
                }

            int C = colors.Count;
            long W = Stuff.Power(C, N * N);

            /// <summary>
            /// 根据遍历 byte[N*N] 提供的 x，y 参数代入 f 从中获取 byte 填充。
            /// </summary>
            /// <returns> N*N长度的byte[] </returns>
            /// <param name="f"> byte取样函数 </param>
            byte[] pattern(Func<int, int, byte> f)
            {
                byte[] result = new byte[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                    {
                        result[x + y * N] = f(x, y);
                    }
                return result;
            };
            /// <summary>
            /// 以xy为起点取长宽为N*N的色块，以xy为起点，dx，dy为宽高。
            /// </summary>
            byte[] patternFromSample(int x, int y) => pattern((dx, dy) => sample[(x + dx) % SMX, (y + dy) % SMY]);
            byte[] rotate(byte[] p) => pattern((x, y) => p[N - 1 - y + x * N]);//向右转
            byte[] reflect(byte[] p) => pattern((x, y) => p[N - 1 - x + y * N]);//x左右翻转

            long index(byte[] p)
            {
                long result = 0;
                long power = 1;
                for (int i = 0; i < p.Length; i++)
                {
                    result += p[p.Length - 1 - i] * power;//乘以power次方，防止index有冲突。
                    power *= C;
                }
                return result;
            };

            byte[] patternFromIndex(long ind)
            {
                long residue = ind;
                long power = W;
                byte[] result = new byte[N * N];

                for (int i = 0; i < result.Length; i++)
                {
                    power /= C;
                    int count = 0;

                    while (residue >= power)
                    {
                        residue -= power;
                        count++;
                    }

                    result[i] = (byte)count;
                }

                return result;
            };

            Dictionary<long, int> weights = new Dictionary<long, int>();
            List<long> ordering = new List<long>();

            //periodicInput含义，如果periodicInput周期性输入为true，那么以xy为起点的N*N色块取值到xy最，到尽头可以从原料图片colors起始（0，0）取。
            //反之，不是周期性，那么xy取最大值预留最大为max(xy)-N，因为非周期性N*N色块的xy>SMYSMX时不能从原料图片colors起始（0，0）取。
            for (int y = 0; y < (periodicInput ? SMY : SMY - N + 1); y++) for (int x = 0; x < (periodicInput ? SMX : SMX - N + 1); x++)
                {
                    //ps:8个N*N大小的索引自sample色块
                    byte[][] ps = new byte[8][];

                    //逐渐顺时针旋转90度的四个方向 0，2，4，6
                    //逐渐对应的反射四个方向 1，3，5，7
                    ps[0] = patternFromSample(x, y);
                    ps[1] = reflect(ps[0]);
                    ps[2] = rotate(ps[0]);
                    ps[3] = reflect(ps[2]);
                    ps[4] = rotate(ps[2]);
                    ps[5] = reflect(ps[4]);
                    ps[6] = rotate(ps[4]);
                    ps[7] = reflect(ps[6]);

                    //symmetry默认值8
                    for (int k = 0; k < symmetry; k++)//对称
                    {
                        long ind = index(ps[k]);  
                        if (weights.ContainsKey(ind))
                        {
                            weights[ind]++;
                        }
                        else
                        {
                            weights.Add(ind, 1);
                            ordering.Add(ind);
                        }
                    }
                }

            T = weights.Count;
            this.ground = (ground + T) % T;
            patterns = new byte[T][];
            base.weights = new double[T];

            int counter = 0;
            foreach (long w in ordering)
            {
                patterns[counter] = patternFromIndex(w);
                base.weights[counter] = weights[w];
                counter++;
            }

            /// <summary>
            /// dx，dy当前p1周围其他色块坐标，判断p1和p2色块交界处像素是否一致
            /// </summary>
            bool agrees(byte[] p1, byte[] p2, int dx, int dy)
            {
                int xmin = dx < 0 ? 0 : dx;
                int xmax = dx < 0 ? dx + N : N;
                int ymin = dy < 0 ? 0 : dy;
                int ymax = dy < 0 ? dy + N : N;
                for (int y = ymin; y < ymax; y++) for (int x = xmin; x < xmax; x++)
                    {
                        if (p1[x + N * y] != p2[x - dx + N * (y - dy)])
                        {
                            return false;
                        }
                    }
                return true;
            };

            propagator = new int[4][][];
            for (int d = 0; d < 4; d++)
            {
                propagator[d] = new int[T][];
                for (int t = 0; t < T; t++)
                {
                    List<int> list = new List<int>();
                    for (int t2 = 0; t2 < T; t2++)
                    {
                        if (agrees(patterns[t], patterns[t2], DX[d], DY[d]))//对d(4)个方向进行匹配，如果相邻tile的边边color一(agrees)那么说明t2添加进可匹配集合。
                        {
                            list.Add(t2);
                        }
                    }
                    //propagator记录T的4个方向相邻匹配的tile色块集合。
                    propagator[d][t] = new int[list.Count];
                    for (int c = 0; c < list.Count; c++)
                    {
                        propagator[d][t][c] = list[c];
                    }
                }
            }
        }
        //periodic == periodicOuput
        protected override bool OnBoundary(int x, int y) => !periodic && (x + N > FMX || y + N > FMY || x < 0 || y < 0);

        public override Texture2D Graphics()
        {
            Texture2D result = new Texture2D(FMX, FMY);
            int[] bitmapData = new int[result.height * result.width];

            if (observed != null)//observed是坍塌运算的结果。
            {
                for (int y = 0; y < FMY; y++)
                {
                    int dy = y < FMY - N + 1 ? 0 : N - 1;
                    for (int x = 0; x < FMX; x++)
                    {
                        int dx = x < FMX - N + 1 ? 0 : N - 1;
                        int obIndex = observed[x - dx + (y - dy) * FMX];
                        Color c = colors[patterns[obIndex][dx + dy * N]];
                        result.SetPixel(x, y, c);
                    }
                }
            }
            else
            {
                for (int i = 0; i < wave.Length; i++)
                {
                    float r = 0, g = 0, b = 0;
                    int x = i % FMX, y = i / FMX;

                    for (int dy = 0; dy < N; dy++) for (int dx = 0; dx < N; dx++)
                        {
                            int sx = x - dx;
                            if (sx < 0) sx += FMX;

                            int sy = y - dy;
                            if (sy < 0) sy += FMY;

                            int s = sx + sy * FMX;

                            if (OnBoundary(sx, sy)) continue;

                            for (int t = 0; t < T; t++) if (wave[s][t])
                                {
                                    Color color = colors[patterns[t][dx + dy * N]];
                                    r += color.r;
                                    g += color.g;
                                    b += color.b;
                                }
                        }
                    result.SetPixel(x, y, new Color(r, g, b));
                }
            }

            result.Apply();
            return result;
        }

        protected override void Clear()
        {
            base.Clear();

            if (ground != 0)
            {
                for (int x = 0; x < FMX; x++)
                {
                    for (int t = 0; t < T; t++)
                    {
                        if (t != ground)
                        {
                            Ban(x + (FMY - 1) * FMX, t);
                        }
                    }
                    for (int y = 0; y < FMY - 1; y++)
                    {
                        Ban(x + y * FMX, ground);
                    }
                }
                Propagate();
            }
        }
    }
}