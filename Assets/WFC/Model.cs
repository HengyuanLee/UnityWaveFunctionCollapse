/*
The MIT License(MIT)
Copyright(c) mxgmn 2016.
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
The software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.
*/

using System;
namespace WFC
{
    abstract class Model
    {
        protected bool[][] wave;//构成效果图的每个图块元素的tile的是否被选中来绘图的bool集合。
        protected int[][][] propagator;//计数，记录每个wave第一维元素中的每个 tile 4个方向的可匹配相邻的 tile 数量集合，它的值来自读取xml的配置，所以值是固定不变的。
        int[][][] compatible;//计数，记录每个wave第一维元素中的每个 tile 的各4个方向的T集合对自己的匹配成功次数而计数，并随着坍塌传递的进行计数递减，所以值是递减变化的。
        protected int[] observed;//坍塌运算完成后被观察的结果，保存每个wave第一维元素中被选中的tile索引用于绘图。

        (int, int)[] stack;
        int stacksize;

        protected Random random;
        protected int FMX, FMY, T;//T==action.count
        protected bool periodic;

        protected double[] weights;
        double[] weightLogWeights;

        int[] sumsOfOnes;
        double sumOfWeights, sumOfWeightLogWeights, startingEntropy;
        double[] sumsOfWeights, sumsOfWeightLogWeights, entropies;

        protected Model(int width, int height)
        {
            FMX = width;
            FMY = height;
        }

        void Init()
        {
            wave = new bool[FMX * FMY][];
            compatible = new int[wave.Length][][];
            for (int i = 0; i < wave.Length; i++)
            {
                wave[i] = new bool[T];
                compatible[i] = new int[T][];
                for (int t = 0; t < T; t++) {
                    compatible[i][t] = new int[4]; 
                }
            }

            weightLogWeights = new double[T];
            sumOfWeights = 0;
            sumOfWeightLogWeights = 0;

            for (int t = 0; t < T; t++)
            {
                weightLogWeights[t] = weights[t] * Math.Log(weights[t]);
                sumOfWeights += weights[t];
                sumOfWeightLogWeights += weightLogWeights[t];
            }
            //以下公式来自熵值法公式，熵值法公式的由来Google一。
            startingEntropy = Math.Log(sumOfWeights) - sumOfWeightLogWeights / sumOfWeights;

            sumsOfOnes = new int[FMX * FMY];
            sumsOfWeights = new double[FMX * FMY];
            sumsOfWeightLogWeights = new double[FMX * FMY];
            entropies = new double[FMX * FMY];

            stack = new(int, int)[wave.Length * T];
            stacksize = 0;
        }

        bool? Observe()
        {
            double min = 1E+3;
            int argmin = -1;

            for (int i = 0; i < wave.Length; i++)
            {
                if (OnBoundary(i % FMX, i / FMX)) continue;

                int amount = sumsOfOnes[i];
                if (amount == 0) return false;//如果第i个格子所有的tile都被ban了，那么失败

                double entropy = entropies[i];
                if (amount > 1 && entropy <= min)//如果wave的第i个元素中候选的tile数量>1,且熵值小于限定值
                {
                    double noise = 1E-6 * random.NextDouble();
                    if (entropy + noise < min)//找出熵值+扰动(noise)后最小的tile的索引argmin
                    {
                        min = entropy + noise;
                        argmin = i;
                    }
                }
            }//根据以上for循环代码，熵值就是用于寻找出wave中第一维元素中最小的argmin索引作为首个目标进行坍塌

            if (argmin == -1) //argmin == -1 那么所有wave的第一维元素的tile被确定了下来，或者进入矛无法在进行坍塌运算。
            {
                observed = new int[FMX * FMY];
                for (int i = 0; i < wave.Length; i++) for (int t = 0; t < T; t++)
                    {
                        if (wave[i][t])
                        {
                            observed[i] = t;//observed 将确定下来的tile的索引记录下来，用于绘图。
                            break;
                        }
                    }
                return true;//返回true开始进行绘图。
            }

            //一下则是所有wave的第一维元素的tile没有被确定了下来，将进行坍塌运算
            double[] distribution = new double[T];
            for (int t = 0; t < T; t++)
            {
                distribution[t] = wave[argmin][t] ? weights[t] : 0;
            }
            int r = distribution.Random(random.NextDouble());//r是随机一个wave[argmin]中为true的tile的索引，随机中的概率跟其xml配置的weight成正比。

            bool[] w = wave[argmin];
            for (int t = 0; t < T; t++)
            {
                if (w[t] != (t == r))//只有w[t]==true,并且t==r时被保留，其它都被ban，而根据以上r的生成，w[t]为false时r!=t, 简写成这种判断形式。
                {
                    Ban(argmin, t); 
                }
            }

            return null;
        }
        //
        protected void Propagate()
        {
            while (stacksize > 0)
            {
                var e1 = stack[stacksize - 1];
                stacksize--;

                int i1 = e1.Item1;
                int x1 = i1 % FMX, y1 = i1 / FMX;

                for (int d = 0; d < 4; d++)
                {
                    int dx = DX[d], dy = DY[d];//获取上下左右个方向的tile
                    int x2 = x1 + dx, y2 = y1 + dy;
                    if (OnBoundary(x2, y2)) continue;

                    if (x2 < 0) {
                        x2 += FMX; 
                    }else if (x2 >= FMX) {
                        x2 -= FMX;
                    }
                    if (y2 < 0) {
                        y2 += FMY; 
                    }else if (y2 >= FMY) {
                        y2 -= FMY; 
                    }

                    int i2 = x2 + y2 * FMX;
                    int[] p = propagator[d][e1.Item2];
                    int[][] compat = compatible[i2];//compatible[i][t][d]

                    for (int l = 0; l < p.Length; l++)
                    {
                        int t2 = p[l];//那么t2就是当前被ban的t(e1.Item2的四个)的相邻匹配的tile。
                        int[] comp = compat[t2];//comp是t2在d方向上的匹配数量，因为t要被ban掉，所以d方向要减去1，如下。

                        comp[d]--;
                        if (comp[d] == 0)//如果d方向已经没有适合t2的tile，==0，那么t2将要被ban掉，如此循环。
                        {
                            Ban(i2, t2);
                        }
                    }
                }
            }
        }

        public bool Run(int seed, int limit)
        {
            if (wave == null) Init();

            Clear();
            random = new Random(seed);

            for (int l = 0; l < limit || limit == 0; l++)
            {
                //为默认值limit==0时，进入Observe()和Propagate()循环。
                //Observe()通过权重算熵值观察，选出要被ban的tile进行ban。
                //Propagate()对被ban的tile的周围4个方向tile，对引用被ban的t的t2的compatible第i/4个方向进行递减1，如t2 compatible第i/4个方向为0那么ban t2如此循环。
                bool? result = Observe();
                if (result != null)
                {
                    return (bool)result;
                }
                Propagate();
            }

            return true;
        }
        /// <summary>
        /// 将wave[i][t]淘汰，并放入stack，将自己被淘汰引起的数据变化在Propagate()函数中传递出去。
        /// </summary>
        protected void Ban(int i, int t)
        {
            wave[i][t] = false;

            int[] comp = compatible[i][t];
            for (int d = 0; d < 4; d++)
            {
                comp[d] = 0;//如果我自己被ban了，那么自己的四个方向的能适合我的tile计数强制设为0，将自己报废。
            }
            stack[stacksize] = (i, t);
            stacksize++;

            sumsOfOnes[i] -= 1;//第i个格子为true的T总数，即没被坍塌淘汰的数量
            sumsOfWeights[i] -= weights[t];
            sumsOfWeightLogWeights[i] -= weightLogWeights[t];

            double sum = sumsOfWeights[i];
            entropies[i] = Math.Log(sum) - sumsOfWeightLogWeights[i] / sum;
        }

        protected virtual void Clear()
        {
            for (int i = 0; i < wave.Length; i++)
            {
                for (int t = 0; t < T; t++)
                {
                    wave[i][t] = true;
                    for (int d = 0; d < 4; d++)
                    {
                        //propagator 记录每个 T 4个方向的可匹配相邻的 T 集合
                        //所以，compatible 为记录第i个T的各4个方向的T集合对自己的匹配成功次数
                        compatible[i][t][d] = propagator[opposite[d]][t].Length;
                    }
                }

                sumsOfOnes[i] = weights.Length;
                sumsOfWeights[i] = sumOfWeights;
                sumsOfWeightLogWeights[i] = sumOfWeightLogWeights;
                entropies[i] = startingEntropy;
            }
        }

        protected abstract bool OnBoundary(int x, int y);
        public abstract UnityEngine.Texture2D Graphics();

        protected static int[] DX = { -1, 0, 1, 0 };
        protected static int[] DY = { 0, 1, 0, -1 };
        static int[] opposite = { 2, 3, 0, 1 };//获取对立面
    }
}
