using BezBukv.Core.Keyboard;
using BezBukv.Core.Platform;

namespace BezBukv.Core.Tests;

public sealed class KeyboardGeometryProfileTests
{
    [Fact]
    public void KeyFHasFilledColumnAlignedNeighbors()
    {
        var neighbors = KeyboardGeometryProfile.PcAnsi.GetNeighbors(PhysicalKey.KeyF);

        Assert.Equal(
            new KeyboardNeighbor[]
            {
                Neighbor(PhysicalKey.KeyE, -1, -1),
                Neighbor(PhysicalKey.KeyR, -1, 0),
                Neighbor(PhysicalKey.KeyT, -1, 1),
                Neighbor(PhysicalKey.KeyD, 0, -1),
                Neighbor(PhysicalKey.KeyG, 0, 1),
                Neighbor(PhysicalKey.KeyC, 1, -1),
                Neighbor(PhysicalKey.KeyV, 1, 0),
                Neighbor(PhysicalKey.KeyB, 1, 1),
            },
            neighbors);
    }

    [Fact]
    public void KeyAIncludesNonLetterNeighborsInFilledRows()
    {
        var neighbors = KeyboardGeometryProfile.PcAnsi.GetNeighbors(PhysicalKey.KeyA);

        Assert.Equal(
            new KeyboardNeighbor[]
            {
                Neighbor(PhysicalKey.Tab, -1, -1),
                Neighbor(PhysicalKey.KeyQ, -1, 0),
                Neighbor(PhysicalKey.KeyW, -1, 1),
                Neighbor(PhysicalKey.CapsLock, 0, -1),
                Neighbor(PhysicalKey.KeyS, 0, 1),
                Neighbor(PhysicalKey.ShiftLeft, 1, -1),
                Neighbor(PhysicalKey.KeyZ, 1, 0),
                Neighbor(PhysicalKey.KeyX, 1, 1),
            },
            neighbors);
    }

    [Fact]
    public void BackquoteCornerHasTruncatedNeighborSet()
    {
        var neighbors = KeyboardGeometryProfile.PcAnsi.GetNeighbors(PhysicalKey.Backquote);

        Assert.Equal(
            new KeyboardNeighbor[]
            {
                Neighbor(PhysicalKey.Digit1, 0, 1),
                Neighbor(PhysicalKey.Tab, 1, 0),
                Neighbor(PhysicalKey.KeyQ, 1, 1),
            },
            neighbors);
    }

    [Fact]
    public void KeyOutsidePcAnsiTypingBlockHasNoNeighbors()
    {
        var neighbors = KeyboardGeometryProfile.PcAnsi.GetNeighbors(PhysicalKey.ArrowUp);

        Assert.Empty(neighbors);
    }

    [Fact]
    public void PcIsoBackslashHasMiddleRowNeighbors()
    {
        var neighbors = KeyboardGeometryProfile.PcIso.GetNeighbors(PhysicalKey.Backslash);

        Assert.Equal(
            new KeyboardNeighbor[]
            {
                Neighbor(PhysicalKey.BracketLeft, -1, -1),
                Neighbor(PhysicalKey.BracketRight, -1, 0),
                Neighbor(PhysicalKey.Quote, 0, -1),
                Neighbor(PhysicalKey.Enter, 0, 1),
                Neighbor(PhysicalKey.Slash, 1, -1),
                Neighbor(PhysicalKey.ShiftRight, 1, 0),
            },
            neighbors);
    }

    [Fact]
    public void KeyOutsidePcIsoTypingBlockHasNoNeighbors()
    {
        var neighbors = KeyboardGeometryProfile.PcIso.GetNeighbors(PhysicalKey.ArrowUp);

        Assert.Empty(neighbors);
    }

    [Fact]
    public void PcIsoContainsSameTypingBlockKeysAsPcAnsi()
    {
        var pcAnsiKeys = Enum.GetValues<PhysicalKey>()
            .Where(key => KeyboardGeometryProfile.PcAnsi.GetNeighbors(key).Count > 0);
        var pcIsoKeys = Enum.GetValues<PhysicalKey>()
            .Where(key => KeyboardGeometryProfile.PcIso.GetNeighbors(key).Count > 0);

        Assert.Equal(pcAnsiKeys, pcIsoKeys);
    }

    [Fact]
    public void EveryNeighborSetFitsAroundItsCenter()
    {
        foreach (var key in Enum.GetValues<PhysicalKey>())
        {
            var neighbors = KeyboardGeometryProfile.PcAnsi.GetNeighbors(key);

            Assert.True(neighbors.Count <= 8);
            Assert.Equal(neighbors.Count, neighbors.Select(neighbor => neighbor.Offset).Distinct().Count());
            Assert.All(
                neighbors,
                neighbor =>
                {
                    Assert.InRange(neighbor.Offset.Row, -1, 1);
                    Assert.InRange(neighbor.Offset.Column, -1, 1);
                    Assert.NotEqual(new KeyboardGridOffset(0, 0), neighbor.Offset);
                });
        }
    }

    private static KeyboardNeighbor Neighbor(
        PhysicalKey key,
        int row,
        int column) =>
        new(key, new KeyboardGridOffset(row, column));
}
