/*
The MIT License(MIT)
Copyright(c) mxgmn 2016.
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
The software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.
*/

using System.Linq;
using System.Xml.Linq;
using System.ComponentModel;
using System.Collections.Generic;

namespace WFC
{

    static class Stuff
    {
        /// <summary>
        /// 在集合a中，由随机数r根据a的每个元素的数值大小正比地获取a的某个元素索引
        /// </summary>
        /// <returns>集合a的某个元素索引值</returns>
        public static int Random(this double[] a, double r)
        {
            double sum = a.Sum();
            for (int j = 0; j < a.Length; j++)
            {
                a[j] /= sum;
            }

            int i = 0;
            double x = 0;

            while (i < a.Length)
            {
                x += a[i];
                if (r <= x)
                {
                    return i;
                }
                i++;
            }

            return 0;
        }
        /// <summary>
        /// 求a的n次方
        /// </summary>
        public static long Power(int a, int n)
        {
            long product = 1;
            for (int i = 0; i < n; i++) product *= a;
            return product;
        }

        public static T Get<T>(this XElement xelem, string attribute, T defaultT = default(T))
        {
            XAttribute a = xelem.Attribute(attribute);
            return a == null ? defaultT : (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(a.Value);
        }

        public static IEnumerable<XElement> Elements(this XElement x, params string[] names) => x.Elements().Where(xelem => names.Any(s => s == xelem.Name));
    }
}
