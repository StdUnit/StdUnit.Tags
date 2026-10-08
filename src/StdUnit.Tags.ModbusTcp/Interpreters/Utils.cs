using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdUnit.Tags.ModbusTcp.Interpreters;

internal static class Utils
{
    /// <summary>
    /// 生成 [0, n-1) 的全排列。<br/>
    /// 核心思想:
    /// 假设已经生成了 0..k-1 的一次排列，现在要把k插入到位置p，则先把p及其右侧往右顺移一格，再把k插入到位置p；<br/>
    /// 如此依次遍历p的位置，即可完成k的在全部位置插入，即从一次 0..k-1 排列，生成了 k次 0..k 排列。<br/>
    /// 如此依次遍历 0..k-1 的所有排列，即可得到 0..k 的所有排列。<br/>
    /// </summary>
    /// <param name="n">n代表有多少个“寄存器点”，通常是2或者4，即32位(2个字长)或者64位(4个字长)</param>
    /// <returns></returns>
    public static ReadOnlyMemory<byte>[] MakePermutations(int n)
    {
        /*
        示例：对于n=4，意味着是拿到[0,3]的排列顺序。
        这可以视作一个递归（或者叫递推），假设我们拿到了[0,2]的所有全排列，对于其任意一次排列，我们可以依次在不同的位置上插入3。
        我们希望第一个出现的排列永远是自然增长的顺序，对于[0,2]，第一个拿到的应该排列是 [0,1,2]，插入4后应该变成 [0,1,2,3]，这意味着我们应该采取倒插的策略。


        推演过程：
        先取 n-1=3 的第一个排列 [0,1,2]，在各位置上依次插入3：
	        位置 3 -> [0,1,2,3]；
	        位置 2 -> [0,1,3,2]；
	        位置 1 -> [0,3,1,2]；
        	位置 0 -> [3,0,1,2]；
 
        然后取 n=3 的第二个排列 [0,2,1]，
	        位置 3 -> [0,2,1,3]；
	        位置 2 -> [0,2,3,1]；
	        位置 1 -> [0,3,2,1]；
        	位置 0 -> [3,0,2,1]；

        ……依此类推。
        每次的结果依次保存下来，就是希望的全排列的顺序。

        */


        // 预分配一张大数组容纳来所有排列：一共有n!种排列，每种排列都有n个元素，故数组大小 = n! * n 
        int total = 1; // 共有多少种排列情况 = n!
        for (int i = 2; i <= n; i++)
        {
            total *= i;
        }
        var flat = new byte[total * n];


        var current = new byte[n];
        int row = 0;

    
        // 这是一个递归函数，负责在 0..k-1 的基础上继续插入 k
        // unit 表示要插入的单元
        void Gen(int unit)
        {
            // 当 unit=n，意味着 0..unit-1 全部放完，达到终止条件
            if (unit == n)
            {
                // 把当前结果拷贝到flat的第 row 行
                Array.Copy(current, 0, flat, row * n, n);
                row++;
                return;
            }

            // 把 unit 依次插入到位置 p
            for (int p = unit; p >= 0; p--)
            {
                // p及其右侧一律向右顺移，然后在位置 p 插入 unit
                for (int i = unit; i > p; i--)
                {
                    current[i] = current[i - 1];
                }
                current[p] = (byte)unit;

                // 递归
                Gen(unit + 1);

                // 回溯：恢复到插入unit之前
                for (int i = p; i < unit; i++)
                {
                    current[i] = current[i + 1];
                }
            }
        }

        // 从0开始递推
        Gen(0);

        // 创建视图数组，每个元素只是指向 flat 的一段
        var views = new ReadOnlyMemory<byte>[total];
        for (int k = 0; k < total; k++)
        {
            views[k] = flat.AsMemory(k * n, n);
        }

        return views;
    }



    /// <summary>
    /// 生成全部合法排布的落位表。<br/> 
    /// 表应在类型初始化时造一次，之后由各解读器实例直接引用。
    /// </summary>
    public static ReadOnlyMemory<byte>[] MakePackings(int unitCount)
    {
        /*
            以 unitCount=2 为例，意味着有两个寄存器点，每个寄存器点有两个字节，总共4个字节。我们希望生成所有可能的字节排列方式。
            两个寄存器点的全排列是 [0,1] 和 [1,0]。
            
            按大端展开后成字节后，落位表是：
            - [0,1] -> [0,1,2,3] : 每个字内部都是大端，字的顺序保持不变：这是一个恒等排布
            - [1,0] -> [2,3,0,1] : 每个字内部都是大端，字的顺序被交换了。
            按小端展开后成字节后，落位表是：
            - [0,1] -> [1,0,3,2] ： 每个字内部都是小端，字的顺序保持不变
            - [1,0] -> [3,2,1,0] ： 每个字内部都是小端，字的顺序被交换了

            再以 unitCount=4 为例，意味着有四个寄存器点，每个寄存器点有两个字节，总共8个字节。我们希望生成所有可能的字节排列方式。
            4个寄存器点的全排列有 24种，
            按大端展开成前4种成字节数组：
            - [0,1,2,3] -> [0,1, 2,3, 4,5, 6,7], 落位表是 [ 0, 1, 2, 3, 4, 5, 6, 7]
            - [0,1,3,2] -> [0,1, 2,3, 6,7, 4,5], 落位表是 [ 0, 1, 2, 3, 6, 7, 4, 5]
            - [0,3,1,2] -> [0,1, 6,7, 2,3, 4,5], 落位表是 [ 0, 1, 4, 5, 6, 7, 2, 3]
            - [3,0,1,2] -> [6,7, 0,1, 2,3, 4,5]，落位表是 [ 2, 3, 4, 5, 6, 7, 0, 1]
         */


        var byteCount = unitCount * 2;
        var permuations = MakePermutations(unitCount);
        var endianArray = new EndianKinds[2] {
            EndianKinds.BigEndian,
            EndianKinds.LittleEndian
        };

        var packings = new List<ReadOnlyMemory<byte>>();
        foreach (var endian in endianArray)
        {
            foreach (var permutation in permuations)
            {
                // 构造落位表：第i个元素值=p，表示字节数组里的第 i 个字节应该处在设备端字节里的位置p；
                var mapping = new byte[byteCount];
                for (var p = 0; p < byteCount; p++)
                {
                    var unit = permutation.Span[p / 2];
                    var inUnit = p % 2;

                    var i = endian == EndianKinds.BigEndian ?
                        (2 * unit) + inUnit :
                        (2 * unit) + 1 - inUnit;

                    mapping[i] = (byte)p;
                }

                packings.Add(mapping.AsMemory());
            }
        }

        // 下标 0（BigEndian + 自然顺序）就是恒等排布，统一用空表表示
        packings[0] = default;
        return packings.ToArray();
    }


}
