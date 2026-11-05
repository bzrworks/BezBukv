using System.Collections.ObjectModel;
using BezBukv.Core.Platform;

namespace BezBukv.Core.Keyboard;

/// <summary>
/// A position in the 3x3 overlay grid relative to its central key.
/// Row and column offsets are each in the range -1 through 1; (0, 0) is reserved for the center.
/// </summary>
public readonly record struct KeyboardGridOffset(int Row, int Column);

/// <summary>
/// A physical key and the cell it occupies relative to the central key.
/// </summary>
public readonly record struct KeyboardNeighbor(
    PhysicalKey Key,
    KeyboardGridOffset Offset);

/// <summary>
/// Platform-independent physical keyboard geometry for one keyboard profile.
/// </summary>
public sealed class KeyboardGeometryProfile
{
    private const int StandardKeyWidth = 8;

    private static readonly IReadOnlyList<KeyboardNeighbor> EmptyNeighbors =
        Array.Empty<KeyboardNeighbor>();

    private readonly IReadOnlyDictionary<PhysicalKey, IReadOnlyList<KeyboardNeighbor>> _neighborsByKey;

    private KeyboardGeometryProfile(IReadOnlyList<IReadOnlyList<KeyDefinition>> rowDefinitions)
    {
        var rowWidths = rowDefinitions
            .Select(row => (IReadOnlyList<int>)row.Select(definition => definition.Width).ToArray())
            .ToArray();
        var neighborGeometry = new KeyboardNeighborGeometry(rowWidths);
        _neighborsByKey = CreateNeighborMap(rowDefinitions, neighborGeometry);
    }

    /// <summary>
    /// The five-row PC ANSI typing block, from the digit row through the modifier/space row.
    /// Function, navigation, and numpad clusters are outside this connected block and therefore
    /// return no neighbors in this profile.
    /// </summary>
    public static KeyboardGeometryProfile PcAnsi { get; } = new(CreatePcAnsiRows());

    /// <summary>
    /// The five-row PC ISO typing block without the 102nd key next to the left Shift key.
    /// Backslash occupies the Caps row and Enter uses the narrow base of its tall ISO shape.
    /// </summary>
    public static KeyboardGeometryProfile PcIso { get; } = new(CreatePcIsoRows());

    /// <summary>
    /// Gets up to eight physical neighbors in deterministic top-to-bottom, left-to-right order.
    /// </summary>
    /// <remarks>
    /// Key centers use eighths of a standard key width so all ANSI quarter-key widths remain exact.
    /// Immediate same-row keys occupy grid columns -1 and +1. On each adjacent row, the key whose
    /// center is horizontally closest to the central key occupies column 0, and its immediate row
    /// neighbors occupy columns -1 and +1. An exact center-distance tie is resolved toward the key
    /// on the right; for example, V is the lower column-0 key for F, yielding C/V/B. This removes
    /// distance-based holes for internal keys while row boundaries still truncate edge grids.
    /// </remarks>
    public IReadOnlyList<KeyboardNeighbor> GetNeighbors(PhysicalKey center) =>
        _neighborsByKey.TryGetValue(center, out var neighbors)
            ? neighbors
            : EmptyNeighbors;

    private static IReadOnlyList<IReadOnlyList<KeyDefinition>> CreatePcAnsiRows() =>
    [
        new KeyDefinition[]
        {
            Key(PhysicalKey.Backquote),
            Key(PhysicalKey.Digit1),
            Key(PhysicalKey.Digit2),
            Key(PhysicalKey.Digit3),
            Key(PhysicalKey.Digit4),
            Key(PhysicalKey.Digit5),
            Key(PhysicalKey.Digit6),
            Key(PhysicalKey.Digit7),
            Key(PhysicalKey.Digit8),
            Key(PhysicalKey.Digit9),
            Key(PhysicalKey.Digit0),
            Key(PhysicalKey.Minus),
            Key(PhysicalKey.Equal),
            Key(PhysicalKey.Backspace, 2.0m),
        },
        new KeyDefinition[]
        {
            Key(PhysicalKey.Tab, 1.5m),
            Key(PhysicalKey.KeyQ),
            Key(PhysicalKey.KeyW),
            Key(PhysicalKey.KeyE),
            Key(PhysicalKey.KeyR),
            Key(PhysicalKey.KeyT),
            Key(PhysicalKey.KeyY),
            Key(PhysicalKey.KeyU),
            Key(PhysicalKey.KeyI),
            Key(PhysicalKey.KeyO),
            Key(PhysicalKey.KeyP),
            Key(PhysicalKey.BracketLeft),
            Key(PhysicalKey.BracketRight),
            Key(PhysicalKey.Backslash, 1.5m),
        },
        new KeyDefinition[]
        {
            Key(PhysicalKey.CapsLock, 1.75m),
            Key(PhysicalKey.KeyA),
            Key(PhysicalKey.KeyS),
            Key(PhysicalKey.KeyD),
            Key(PhysicalKey.KeyF),
            Key(PhysicalKey.KeyG),
            Key(PhysicalKey.KeyH),
            Key(PhysicalKey.KeyJ),
            Key(PhysicalKey.KeyK),
            Key(PhysicalKey.KeyL),
            Key(PhysicalKey.Semicolon),
            Key(PhysicalKey.Quote),
            Key(PhysicalKey.Enter, 2.25m),
        },
        new KeyDefinition[]
        {
            Key(PhysicalKey.ShiftLeft, 2.25m),
            Key(PhysicalKey.KeyZ),
            Key(PhysicalKey.KeyX),
            Key(PhysicalKey.KeyC),
            Key(PhysicalKey.KeyV),
            Key(PhysicalKey.KeyB),
            Key(PhysicalKey.KeyN),
            Key(PhysicalKey.KeyM),
            Key(PhysicalKey.Comma),
            Key(PhysicalKey.Period),
            Key(PhysicalKey.Slash),
            Key(PhysicalKey.ShiftRight, 2.75m),
        },
        new KeyDefinition[]
        {
            Key(PhysicalKey.ControlLeft, 1.25m),
            Key(PhysicalKey.MetaLeft, 1.25m),
            Key(PhysicalKey.AltLeft, 1.25m),
            Key(PhysicalKey.Space, 6.25m),
            Key(PhysicalKey.AltRight, 1.25m),
            Key(PhysicalKey.MetaRight, 1.25m),
            Key(PhysicalKey.ContextMenu, 1.25m),
            Key(PhysicalKey.ControlRight, 1.25m),
        },
    ];

    private static IReadOnlyList<IReadOnlyList<KeyDefinition>> CreatePcIsoRows() =>
    [
        new KeyDefinition[]
        {
            Key(PhysicalKey.Backquote),
            Key(PhysicalKey.Digit1),
            Key(PhysicalKey.Digit2),
            Key(PhysicalKey.Digit3),
            Key(PhysicalKey.Digit4),
            Key(PhysicalKey.Digit5),
            Key(PhysicalKey.Digit6),
            Key(PhysicalKey.Digit7),
            Key(PhysicalKey.Digit8),
            Key(PhysicalKey.Digit9),
            Key(PhysicalKey.Digit0),
            Key(PhysicalKey.Minus),
            Key(PhysicalKey.Equal),
            Key(PhysicalKey.Backspace, 2.0m),
        },
        new KeyDefinition[]
        {
            Key(PhysicalKey.Tab, 1.5m),
            Key(PhysicalKey.KeyQ),
            Key(PhysicalKey.KeyW),
            Key(PhysicalKey.KeyE),
            Key(PhysicalKey.KeyR),
            Key(PhysicalKey.KeyT),
            Key(PhysicalKey.KeyY),
            Key(PhysicalKey.KeyU),
            Key(PhysicalKey.KeyI),
            Key(PhysicalKey.KeyO),
            Key(PhysicalKey.KeyP),
            Key(PhysicalKey.BracketLeft),
            Key(PhysicalKey.BracketRight),
        },
        new KeyDefinition[]
        {
            Key(PhysicalKey.CapsLock, 1.75m),
            Key(PhysicalKey.KeyA),
            Key(PhysicalKey.KeyS),
            Key(PhysicalKey.KeyD),
            Key(PhysicalKey.KeyF),
            Key(PhysicalKey.KeyG),
            Key(PhysicalKey.KeyH),
            Key(PhysicalKey.KeyJ),
            Key(PhysicalKey.KeyK),
            Key(PhysicalKey.KeyL),
            Key(PhysicalKey.Semicolon),
            Key(PhysicalKey.Quote),
            Key(PhysicalKey.Backslash),
            Key(PhysicalKey.Enter, 1.25m),
        },
        new KeyDefinition[]
        {
            Key(PhysicalKey.ShiftLeft, 2.25m),
            Key(PhysicalKey.KeyZ),
            Key(PhysicalKey.KeyX),
            Key(PhysicalKey.KeyC),
            Key(PhysicalKey.KeyV),
            Key(PhysicalKey.KeyB),
            Key(PhysicalKey.KeyN),
            Key(PhysicalKey.KeyM),
            Key(PhysicalKey.Comma),
            Key(PhysicalKey.Period),
            Key(PhysicalKey.Slash),
            Key(PhysicalKey.ShiftRight, 2.75m),
        },
        new KeyDefinition[]
        {
            Key(PhysicalKey.ControlLeft, 1.25m),
            Key(PhysicalKey.MetaLeft, 1.25m),
            Key(PhysicalKey.AltLeft, 1.25m),
            Key(PhysicalKey.Space, 6.25m),
            Key(PhysicalKey.AltRight, 1.25m),
            Key(PhysicalKey.MetaRight, 1.25m),
            Key(PhysicalKey.ContextMenu, 1.25m),
            Key(PhysicalKey.ControlRight, 1.25m),
        },
    ];

    private static KeyDefinition Key(PhysicalKey key, decimal width = 1.0m) =>
        new(key, checked((int)(width * StandardKeyWidth)));

    private static IReadOnlyDictionary<PhysicalKey, IReadOnlyList<KeyboardNeighbor>> CreateNeighborMap(
        IReadOnlyList<IReadOnlyList<KeyDefinition>> rowDefinitions,
        KeyboardNeighborGeometry neighborGeometry)
    {
        var neighborsByKey = new Dictionary<PhysicalKey, IReadOnlyList<KeyboardNeighbor>>();
        var knownKeys = new HashSet<PhysicalKey>();

        for (var rowIndex = 0; rowIndex < rowDefinitions.Count; rowIndex++)
        {
            var row = rowDefinitions[rowIndex];
            for (var keyIndex = 0; keyIndex < row.Count; keyIndex++)
            {
                var center = row[keyIndex];
                if (!knownKeys.Add(center.Key))
                {
                    throw new InvalidOperationException(
                        $"Physical key {center.Key} occurs more than once in a geometry profile.");
                }

                var neighbors = neighborGeometry
                    .GetNeighbors(new KeyboardCellPosition(rowIndex, keyIndex))
                    .Select(neighbor => new KeyboardNeighbor(
                        rowDefinitions[neighbor.Position.Row][neighbor.Position.Index].Key,
                        neighbor.Offset))
                    .ToArray();

                neighborsByKey.Add(center.Key, Array.AsReadOnly(neighbors));
            }
        }

        return new ReadOnlyDictionary<PhysicalKey, IReadOnlyList<KeyboardNeighbor>>(neighborsByKey);
    }

    private readonly record struct KeyDefinition(PhysicalKey Key, int Width);
}
