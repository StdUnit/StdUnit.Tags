using StdUnit.Tags.ModbusTcp.Interpreters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

public class PermutationTests
{
    /// <summary>
    /// 0个点的全排列
    /// </summary>
    [Fact]
    public void TestMakePermutationsFor0Unit()
    {
        var arr = Utils.MakePermutations(0);
        Assert.Single(arr);
        var item = arr[0];
        Assert.True(item.IsEmpty);
    }

    /// <summary>
    /// 1个点的全排列
    /// </summary>
    [Fact]
    public void TestMakePermutationsFor1Unit()
    {
        var arr = Utils.MakePermutations(1);
        Assert.Single(arr);

        var item = arr[0];
        Assert.Equal(1, item.Length);
        Assert.Equal(0, item.Span[0]);
    }

    /// <summary>
    /// 2个点的全排列
    /// </summary>
    [Fact]
    public void TestMakePermutationsFor2Units()
    {
        var arr = Utils.MakePermutations(2);
        Assert.Equal(2, arr.Length);     // 2!


        var targets = new byte[2][] {
            new byte[] { 0, 1 },
            new byte[] { 1, 0 },
        };

        for (int row = 0; row < arr.Length; row++)
        {
            var item = arr[row];
            Assert.Equal(2, item.Length);    // 每种排列有 n 个元素

            var target = targets[row];
            for (int i = 0; i < item.Length; i++)
            {
                var left = item.Span[i];
                var right = target[i];
                Assert.Equal(left, right);
            }
        }
    }

    /// <summary>
    /// 3个点的全排列
    /// </summary>
    [Fact]
    public void TestMakePermutationsFor3Units()
    {
        var arr = Utils.MakePermutations(3);
        Assert.Equal(6, arr.Length);     // 3!


        var targets = new byte[6][] {
            new byte[] { 0, 1, 2 },
            new byte[] { 0, 2, 1 },
            new byte[] { 2, 0, 1 },
            new byte[] { 1, 0, 2 },
            new byte[] { 1, 2, 0 },
            new byte[] { 2, 1, 0 },
        };

        for (int row = 0; row < arr.Length; row++)
        {
            var item = arr[row];
            Assert.Equal(3, item.Length);    // 每种排列有 n 个元素

            var target = targets[row];
            for (int i = 0; i < item.Length; i++)
            {
                var left = item.Span[i];
                var right = target[i];
                Assert.Equal(left, right);
            }
        }
    }


    /// <summary>
    /// 4个点的全排列
    /// </summary>
    [Fact]
    public void TestMakePermutationsFor4Units()
    {
        var arr = Utils.MakePermutations(4);
        Assert.Equal(24, arr.Length);     // 4!


        var targets = new byte[24][] {
             new byte[]{0,1,2,3},
             new byte[]{0,1,3,2},
             new byte[]{0,3,1,2},
             new byte[]{3,0,1,2},

             new byte[]{0,2,1,3},
             new byte[]{0,2,3,1},
             new byte[]{0,3,2,1},
             new byte[]{3,0,2,1},

             new byte[]{2,0,1,3},
             new byte[]{2,0,3,1},
             new byte[]{2,3,0,1},
             new byte[]{3,2,0,1},

             new byte[]{1,0,2,3},
             new byte[]{1,0,3,2},
             new byte[]{1,3,0,2},
             new byte[]{3,1,0,2},

             new byte[]{1,2,0,3},
             new byte[]{1,2,3,0},
             new byte[]{1,3,2,0},
             new byte[]{3,1,2,0},

             new byte[]{2,1,0,3},
             new byte[]{2,1,3,0},
             new byte[]{2,3,1,0},
             new byte[]{3,2,1,0},
        };

        for (int row = 0; row < arr.Length; row++)
        {
            var item = arr[row];
            Assert.Equal(4, item.Length);    // 每种排列有 n 个元素

            var target = targets[row];
            for (int i = 0; i < item.Length; i++)
            {
                var left = item.Span[i];
                var right = target[i];
                Assert.Equal(left, right);
            }
        }
    }

}
