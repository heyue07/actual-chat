using ActualChat.Mathematics.Internal;

namespace ActualChat.Core.UnitTests;

public class LinearMapTest : TestBase
{
    public LinearMapTest(ITestOutputHelper @out) : base(@out) { }

    [Fact]
    public void BasicTest()
    {
        var map = new LinearMap(0, 1).AssertValid();
        map.TryMap(-1).Should().BeNull();
        map.TryMap(1).Should().BeNull();
        map.TryMap(0).Should().Be(1.0f);

        map = new LinearMap(0, 1, 1, 3).AssertValid();
        map.TryMap(-0.1f).Should().BeNull();
        map.TryMap(1.1f).Should().BeNull();
        map.TryMap(0).Should().Be(1.0f);
        map.TryMap(1).Should().Be(3.0f);
        map.TryMap(0.5f).Should().Be(2.0f);

        map = new LinearMap(0, 1, 1, 3, 11, 4).AssertValid();
        map.TryMap(-0.1f).Should().BeNull();
        map.TryMap(11.1f).Should().BeNull();
        map.TryMap(0).Should().Be(1.0f);
        map.TryMap(1).Should().Be(3.0f);
        map.TryMap(11).Should().Be(4.0f);
        map.TryMap(0.5f).Should().Be(2.0f);
        map.TryMap(6).Should().Be(3.5f);

        var map1 = map.PassThroughAllSerializers(Out);
        map1.Data.Should().Equal(map.Data);
    }
    [Fact]
    public void ActualMapTest()
    {
        var json = "{\"sourcePoints\":[0,4,18,20,25,27,37,46,53,57,64,74,81,93,98],\"targetPoints\":[0,1.8,2.4,3.2,3.4,4.2,4.3,5.4,5.5,6.9,7.4,7.6,8.9,9.9,10.5]}";
        var oldMap = SystemJsonSerializer.Default.Read<OldLinearMap>(json);
        var map = oldMap.ToLinearMap();
        map.Length.Should().BeGreaterThan(10);
        var last = 0f;
        for (var i = 0; i <= 98; i++) {
            var current = map.TryMap(i)!.Value;
            current.Should().BeGreaterOrEqualTo(last);
            last = current;
        }
    }

    [Fact]
    public void WrongMapTest()
    {
        Assert.Throws<InvalidOperationException>(() => {
            new LinearMap(1, 1, 1, 2).AssertValid();
        });
        new LinearMap(1, 2, 2, 2).AssertValid();
        Assert.Throws<InvalidOperationException>(() => {
            new LinearMap(1, 2, 2, 2).AssertValid(true);
        });
        Assert.Throws<InvalidOperationException>(() => {
            new LinearMap(1, 2, 2, 1).AssertValid(true);
        });
    }
}
