/*
The MIT License(MIT)
Copyright(c) mxgmn 2016.
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
The software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.
*/

using System;
using System.Xml.Linq;
using UnityEngine;

namespace WFC
{

    public class WFCMain : MonoBehaviour
    {
        public TextAsset XML;

        private void Awake()
        {

            System.Random random = new System.Random();
            XDocument xdoc = XDocument.Parse(XML.text);

            int counter = 1;
            foreach (XElement xelem in xdoc.Root.Elements("simpletiled", "overlapping"))

            {
                Model model;
                string name = xelem.Get<string>("name");
                string subName = string.Empty;
                Debug.Log($"< {name}");

                if (xelem.Name == "overlapping")
                {
                    model = new OverlappingModel(
                        name,
                        xelem.Get("N", 2),//N每个overlapping类型的tile的像素大小，即宽高N*N。
                        xelem.Get("width", 48),//结果绘图的像素宽，下同理。
                        xelem.Get("height", 48),
                        xelem.Get("periodicInput", true),//从原料图片中取N*N tile大小时是否可以周期性获取。
                        xelem.Get("periodic", false),
                        xelem.Get("symmetry", 8),
                        xelem.Get("ground", 0)
                    );
                }
                else if (xelem.Name == "simpletiled")
                {
                    subName = xelem.Get<string>("subset");//subset，结果绘图生成设置。
                    model = new SimpleTiledModel(
                        name,
                        subName,
                        xelem.Get("width", 10),
                        xelem.Get("height", 10),
                        xelem.Get("periodic", false),
                        xelem.Get("black", false)
                    );
                }
                else
                {
                    continue;
                }

                for (int i = 0; i < xelem.Get("screenshots", 2); i++)
                {
                    for (int k = 0; k < 10; k++)
                    {
                        Debug.Log("> ");
                        int seed = random.Next();
                        bool finished = model.Run(seed, xelem.Get("limit", 0));
                        if (finished)
                        {
                            Debug.Log("DONE");
                            Texture2D texture2D = model.Graphics();

                            Sprite sprite = Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), Vector2.zero);
                            GameObject go = new GameObject($"{name} {subName}");
                            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                            sr.sprite = sprite;
                            break;
                        }
                        else Debug.Log("CONTRADICTION");
                    }
                }

                counter++;
            }
        }
    }
}
