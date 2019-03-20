/*
The MIT License(MIT)
Copyright(c) mxgmn 2016.
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
The software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.
*/

using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace WFC
{

    class SimpleTiledModel : Model
    {
        List<Texture2D> tiles;
        List<string> tilenames;
        int tilesize;
        bool black;

        private string graphicsName = String.Empty;

        public SimpleTiledModel(string name, string subsetName, int width, int height, bool periodic, bool black) : base(width, height)
        {
            this.graphicsName = $"{name} {subsetName}";
            this.periodic = periodic;
            this.black = black;
            string dataXmlpath = $"{Application.dataPath}/Resources/samples/{name}/data.xml";
            XElement xroot = XDocument.Load(dataXmlpath).Root;
            tilesize = xroot.Get("size", 16);
            bool unique = xroot.Get("unique", false);

            List<string> subset = null;
            if (subsetName != default(string))
            {
                XElement xsubset = xroot.Element("subsets").Elements("subset").FirstOrDefault(x => x.Get<string>("name") == subsetName);
                if (xsubset == null){
                    Debug.LogError($"ERROR: subset {subsetName} is not found"); 
                }else {
                    subset = xsubset.Elements("tile").Select(x => x.Get<string>("name")).ToList();
                }
            }

            tiles = new List<Texture2D>();
            tilenames = new List<string>();
            var tempStationary = new List<double>();

            List<int[]> action = new List<int[]>();//第一维记录xml 配置上的tilename(图片名称)顺时针旋转90度cardinality次后在变量tile(List<Texture2D>)上的索引位、第二维记录该旋转后的tile 8个方向后的索引。
            Dictionary<string, int> firstOccurrence = new Dictionary<string, int>();
           
            foreach (XElement xtile in xroot.Element("tiles").Elements("tile"))
            {
                string tilename = xtile.Get<string>("name");
                if (subset != null && !subset.Contains(tilename)) continue;

                Func<int, int> a, b;//对tile获取a正面，b反面
                int cardinality;

                char sym = xtile.Get("symmetry", 'X');
                if (sym == 'L')
                {
                    //a 0123 对应 b 1032
                    cardinality = 4;
                    a = i => (i + 1) % 4; //a正面，“L”形原材料图片经翻转后有4(cardinality=4)种不同情形图片，后面代码说明i是第cardinality次。
                    b = i => i % 2 == 0 ? i + 1 : i - 1;//b对称面，是相对a面的二次操作获取结果。
                }
                else if (sym == 'T')
                {
                    //a 0123 对应 b 0321
                    cardinality = 4;
                    a = i => (i + 1) % 4;
                    b = i => i % 2 == 0 ? i : 4 - i;
                }
                else if (sym == 'I')
                {
                    cardinality = 2;
                    a = i => 1 - i;//a正面，“I”形对称图左右或上下对称，可用同一张，所以有2(cardinality=2)种不同情形。
                    b = i => i;
                }
                else if (sym == '\\')
                {
                    cardinality = 2;
                    a = i => 1 - i;
                    b = i => 1 - i;
                }
                else
                {
                    cardinality = 1;
                    a = i => i;
                    b = i => i;
                }

                T = action.Count;//action依赖firstOccurrence用tilename为key记录tile在action中的起始第一个索引位置。
                firstOccurrence.Add(tilename, T);//记录此tileName的起始index, kv(tilename, t)。

                int[][] map = new int[cardinality][];
                for (int t = 0; t < cardinality; t++)
                {
                    map[t] = new int[8];//map记录每次翻转tile后的每个tile的八个方向所使用的tile在action中的索引。

                    map[t][0] = t;//map[t][0]是翻转后非对称的衍生tile
                    map[t][1] = a(t);//根据上个if代码块，对称性定义的a获取第t次翻转用的index。
                    map[t][2] = a(a(t));
                    map[t][3] = a(a(a(t)));
                    map[t][4] = b(t);
                    map[t][5] = b(a(t));
                    map[t][6] = b(a(a(t)));
                    map[t][7] = b(a(a(a(t))));

                    for (int s = 0; s < 8; s++)
                    {
                        map[t][s] += T;//索引递增叠加。
                    }

                    action.Add(map[t]);//所以action长度为 配置上的tiles的各自的tile的cardinality*8再累加个。
                }

                if (unique)
                {
                    for (int t = 0; t < cardinality; t++)
                    {
                        string imagePath = $"samples/{name}/{tilename} {t}";
                        Texture2D tile = Resources.Load<Texture2D>(imagePath);
                        if(tile == null){
                            Debug.LogError($"load null {imagePath}");
                        }
                        tiles.Add(tile);
                        tilenames.Add($"{tilename} {t}");
                    }
                }
                else
                {
                    string imagePath = $"samples/{name}/{tilename}";
                    Texture2D tile = Resources.Load<Texture2D>(imagePath);
                    tiles.Add(tile); 
                    tilenames.Add($"{tilename} 0");

                    for (int t = 1; t < cardinality; t++)//根据cardinality次数将tile顺时针旋转。
                    {
                        tiles.Add(Rotate(tiles[T + t - 1]));
                        string newTilename = $"{tilename} {t}";
                        tilenames.Add(newTilename);
                    }
                }

                for (int t = 0; t < cardinality; t++) tempStationary.Add(xtile.Get("weight", 1.0f));
            }

            T = action.Count;
            weights = tempStationary.ToArray();

            //propagator，从tempPropagator中提取值为true的T*T，即相邻的tile
            //propagator 记录每个 T 4个方向的可匹配相邻的 T 集合
            propagator = new int[4][][];
            var tempPropagator = new bool[4][][];//tempPropagator罗列出每个格子4个方向所能放的所有可(4*T*T)， 能够相邻匹配的action的index
            for (int d = 0; d < 4; d++)
            {
                tempPropagator[d] = new bool[T][];
                propagator[d] = new int[T][];
                for (int t = 0; t < T; t++)
                {
                    tempPropagator[d][t] = new bool[T];
                }
            }

            foreach (XElement xneighbor in xroot.Element("neighbors").Elements("neighbor"))
            {
                string[] left = xneighbor.Get<string>("left").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string[] right = xneighbor.Get<string>("right").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (subset != null && (!subset.Contains(left[0]) || !subset.Contains(right[0]))) continue;

                //action的坐标索引依赖firstOccurrence
                int firstLeft = firstOccurrence[left[0]];
                int leftIndex = left.Length == 1 ? 0 : int.Parse(left[1]);

                int firstRight = firstOccurrence[right[0]];
                int rightIndex = right.Length == 1 ? 0 : int.Parse(right[1]);

                //根据neighbors从tempPropagator中获取左右匹配的tile的配对，设置为true，以便传给propagator
                int L = action[firstLeft][leftIndex];
                int D = action[L][1];
                int R = action[firstRight][rightIndex];
                int U = action[R][1];

                tempPropagator[0][R][L] = true;
                tempPropagator[0][action[R][6]][action[L][6]] = true;//方向0对应了翻转后的方向6，下同理
                tempPropagator[0][action[L][4]][action[R][4]] = true;
                tempPropagator[0][action[L][2]][action[R][2]] = true;

                tempPropagator[1][U][D] = true;
                tempPropagator[1][action[D][6]][action[U][6]] = true;
                tempPropagator[1][action[U][4]][action[D][4]] = true;
                tempPropagator[1][action[D][2]][action[U][2]] = true;
            }
            //左右方向互换
            for (int t2 = 0; t2 < T; t2++) for (int t1 = 0; t1 < T; t1++)
                {
                    tempPropagator[2][t2][t1] = tempPropagator[0][t1][t2];
                    tempPropagator[3][t2][t1] = tempPropagator[1][t1][t2];
                }

            List<int>[][] sparsePropagator = new List<int>[4][];
            for (int d = 0; d < 4; d++)
            {
                sparsePropagator[d] = new List<int>[T];
                for (int t = 0; t < T; t++)
                {
                    sparsePropagator[d][t] = new List<int>();
                }
            }

            for (int d = 0; d < 4; d++) for (int t1 = 0; t1 < T; t1++)
                {
                    List<int> sp = sparsePropagator[d][t1];
                    bool[] tp = tempPropagator[d][t1];

                    for (int t2 = 0; t2 < T; t2++)
                    {
                        if (tp[t2])
                            sp.Add(t2);
                    }

                    int ST = sp.Count;
                    propagator[d][t1] = new int[ST];
                    for (int st = 0; st < ST; st++) {
                        propagator[d][t1][st] = sp[st]; //propagator记录每个tile4个方向各自所适配的tile集合。
                    }
                }
        }
        /// <summary>
        /// 将 texture 复制一份，并向右旋转90度
        /// </summary>
        /// <returns>返回复制并旋转后texture</returns>
        private Texture2D Rotate(Texture2D texture)
        {
            Texture2D result = new Texture2D(texture.width, texture.height);
            for (int x = 0; x < texture.width; x++) for (int y = 0; y < texture.height; y++)
            {
                int rx = y;
                int ry = texture.height - x - 1;
                result.SetPixel(x, y, texture.GetPixel(rx, ry));
            }
            result.Apply();
            return result;
        }
        protected override bool OnBoundary(int x, int y) => !periodic && (x < 0 || y < 0 || x >= FMX || y >= FMY);

        public override Texture2D Graphics()
        {
            Texture2D result = new Texture2D(FMX * tilesize, FMY * tilesize);
            if (observed != null)
            {
                //observed是已经完成坍塌，确定了绘图用的tile的集合。
                for (int x = 0; x < FMX; x++) for (int y = 0; y < FMY; y++)
                    {
                    int index = observed[x + y * FMX];
                    Texture2D tile = tiles[index];

                    for (int x2 = 0; x2 < tile.width; x2++)
                        for (int y2 = 0; y2 < tile.height; y2++){
                                int px = x * tilesize + x2;
                                int py = (tilesize - 1 - y - tile.height) * tilesize + y2;
                                result.SetPixel(px, py, tile.GetPixel(x2, y2));
                            }
                    }

            }
            else
            {
                int tileheight = tiles[0].height;
                //如果坍塌函数失败不能再进行下去，那么绘图单位tile的色值是对所有有可能的tile的颜色进行混合。
                for (int x = 0; x < FMX; x++) for (int y = 0; y < FMY; y++)
                    {
                        bool[] a = wave[x + y * FMX];
                        int amount = (from b in a where b select 1).Sum();
                        double lambda = 1.0 / (from t in Enumerable.Range(0, T) where a[t] select weights[t]).Sum();

                        for (int yt = 0; yt < tilesize; yt++) for (int xt = 0; xt < tilesize; xt++)
                            {
                                int px = x * tilesize + xt;
                                int py = (tilesize - 1 - y - tileheight) * tilesize + yt;

                                if (black && amount == T) {
                                    result.SetPixel(px, py, Color.clear);
                                }else{
                                    double r = 0, g = 0, b = 0;
                                    for (int t = 0; t < T; t++)
                                    {
                                        if (a[t])
                                        {
                                            Texture2D texture = tiles[t];
                                            Color c = texture.GetPixel(xt, yt);
                                            r += (double)c.r * weights[t] * lambda;
                                            g += (double)c.g * weights[t] * lambda;
                                            b += (double)c.b * weights[t] * lambda;
                                        }
                                        result.SetPixel(px, py, new Color((float)r, (float)g, (float)b));
                                    }
                                }
                            }
                    }
            }
            result.Apply();
            return result;
        }

        public string TextOutput()
        {
            var result = new System.Text.StringBuilder();

            for (int y = 0; y < FMY; y++)
            {
                for (int x = 0; x < FMX; x++) result.Append($"{tilenames[observed[x + y * FMX]]}, ");
                result.Append(Environment.NewLine);
            }

            return result.ToString();
        }
    }
}
