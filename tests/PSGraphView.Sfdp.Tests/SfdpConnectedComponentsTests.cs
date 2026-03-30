using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpConnectedComponentsTests
{
    [Fact]
    public void Find_ReturnsExpectedComponents()
    {
        var graph = new SfdpCsrGraph(
            5,
            [0, 1, 2, 2, 3, 4],
            [1, 0, 4, 3]);

        var result = SfdpConnectedComponents.Find(graph);

        Assert.Equal(3, result.ComponentCount);
        Assert.Equal(0, result.ComponentByNode[0]);
        Assert.Equal(0, result.ComponentByNode[1]);
        Assert.Equal(1, result.ComponentByNode[2]);
        Assert.Equal(2, result.ComponentByNode[3]);
        Assert.Equal(2, result.ComponentByNode[4]);
    }
}
