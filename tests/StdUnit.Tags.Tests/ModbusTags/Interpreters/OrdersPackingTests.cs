using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

public class OrdersPackingTests
{
    [Fact]
    public void TestPackings2()
    {
        var packings = ModbusTcp.Interpreters.Utils.MakePackings(2);
        Assert.Equal(4, packings.Length);

        // BE
        //      [0,1] -> [0,1, 2,3]
        //      [1,0] -> [2,3, 0,1]
        // LE
        //      [0,1] -> [1,0, 3,2]
        //      [1,0] -> [3,2, 1,0]

        var targets = new byte[4][] { 
            new byte[] { 0, 1, 2, 3 },   // 对应的实际位置会被填充成 default
            new byte[] { 2, 3, 0, 1 },
            new byte[] { 1, 0, 3, 2 },
            new byte[] { 3, 2, 0, 1 },
        };
        for (var i = 0; i < packings.Length - 1; i++)
        {
            var item = packings[i];
            if (i == 0)
            {
                Assert.True(item.IsEmpty);
            }
            else
            {
                Assert.Equal(4, item.Length);
            }


            var target = targets[i];
            for(var j = 0; j< item.Length; j++)
            {
                var left = item.Span[j];
                var right = target[j];
                Assert.Equal(left, right);
            }
        }
    }

    [Fact]
    public void TestPackings4()
    {
        var packings = ModbusTcp.Interpreters.Utils.MakePackings(4);
        Assert.Equal(48, packings.Length);
        Assert.True(packings[0].IsEmpty);

        var targetsBE = new byte[24][] {
            new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, },  // 对应的实际位置会被填充成 default
            new byte[] { 0, 1, 2, 3, 6, 7, 4, 5, },
            new byte[] { 0, 1, 4, 5, 6, 7, 2, 3, },
            new byte[] { 2, 3, 4, 5, 6, 7, 0, 1, },

            new byte[] { 0, 1, 4, 5, 2, 3, 6, 7, },
            new byte[] { 0, 1, 6, 7, 2, 3, 4, 5, },
            new byte[] { 0, 1, 6, 7, 4, 5, 2, 3, },
            new byte[] { 2, 3, 6, 7, 4, 5, 0, 1, },

            new byte[] { 2, 3, 4, 5, 0, 1, 6, 7, },
            new byte[] { 2, 3, 6, 7, 0, 1, 4, 5, },
            new byte[] { 4, 5, 6, 7, 0, 1, 2, 3, },
            new byte[] { 4, 5, 6, 7, 2, 3, 0, 1, },

            new byte[] { 2, 3, 0, 1, 4, 5, 6, 7, },
            new byte[] { 2, 3, 0, 1, 6, 7, 4, 5, },
            new byte[] { 4, 5, 0, 1, 6, 7, 2, 3, },
            new byte[] { 4, 5, 2, 3, 6, 7, 0, 1, },

            new byte[] { 4, 5, 0, 1, 2, 3, 6, 7, },
            new byte[] { 6, 7, 0, 1, 2, 3, 4, 5, },
            new byte[] { 6, 7, 0, 1, 4, 5, 2, 3, },
            new byte[] { 6, 7, 2, 3, 4, 5, 0, 1, },

            new byte[] { 4, 5, 2, 3, 0, 1, 6, 7, },
            new byte[] { 6, 7, 2, 3, 0, 1, 4, 5, },
            new byte[] { 6, 7, 4, 5, 0, 1, 2, 3, },
            new byte[] { 6, 7, 4, 5, 2, 3, 0, 1, },
        };

        var targetsLE = new byte[24][] {
            new byte[] { 1, 0, 3, 2, 5, 4, 7, 6, }, 
            new byte[] { 1, 0, 3, 2, 7, 6, 5, 4, }, 
            new byte[] { 1, 0, 5, 4, 7, 6, 3, 2, }, 
            new byte[] { 3, 2, 5, 4, 7, 6, 1, 0, }, 

            new byte[] { 1, 0, 5, 4, 3, 2, 7, 6, }, 
            new byte[] { 1, 0, 7, 6, 3, 2, 5, 4, }, 
            new byte[] { 1, 0, 7, 6, 5, 4, 3, 2, }, 
            new byte[] { 3, 2, 7, 6, 5, 4, 1, 0, }, 

            new byte[] { 3, 2, 5, 4, 1, 0, 7, 6, }, 
            new byte[] { 3, 2, 7, 6, 1, 0, 5, 4, }, 
            new byte[] { 5, 4, 7, 6, 1, 0, 3, 2, }, 
            new byte[] { 5, 4, 7, 6, 3, 2, 1, 0, }, 

            new byte[] { 3, 2, 1, 0, 5, 4, 7, 6, }, 
            new byte[] { 3, 2, 1, 0, 7, 6, 5, 4, }, 
            new byte[] { 5, 4, 1, 0, 7, 6, 3, 2, }, 
            new byte[] { 5, 4, 3, 2, 7, 6, 1, 0, }, 

            new byte[] { 5, 4, 1, 0, 3, 2, 7, 6, }, 
            new byte[] { 7, 6, 1, 0, 3, 2, 5, 4, }, 
            new byte[] { 7, 6, 1, 0, 5, 4, 3, 2, }, 
            new byte[] { 7, 6, 3, 2, 5, 4, 1, 0, }, 

            new byte[] { 5, 4, 3, 2, 1, 0, 7, 6, }, 
            new byte[] { 7, 6, 3, 2, 1, 0, 5, 4, }, 
            new byte[] { 7, 6, 5, 4, 1, 0, 3, 2, }, 
            new byte[] { 7, 6, 5, 4, 3, 2, 1, 0, },
        };

        var targets = targetsBE.Concat(targetsLE).ToArray();

        for (var i = 0; i < packings.Length ; i++)
        {
            var item = packings[i];
            if (i == 0)
            {
                Assert.True(item.IsEmpty);
            }
            else
            {
                Assert.Equal(8, item.Length);
            }
            var target = targets[i];
            for (var j = 0; j < item.Length; j++)
            {
                var left = item.Span[j];
                var right = target[j];
                Assert.Equal(left, right);
            }
        }
    }
}
