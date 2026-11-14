using Zodiac.Content.Authoring;
using Zodiac.Framework;

namespace Fft.Editor;

internal sealed class MovementPreviewPanel : Panel
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal EditorMovementPreview? Value { get; set; }
    internal MovementPreviewPanel() { DoubleBuffered = true; BackColor = Color.White; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Value is not { } value)
        {
            e.Graphics.DrawString("Validate and preview to compare source and authored movement.", Font, Brushes.DimGray, 10, 10);
            return;
        }
        int cell = Math.Max(9, Math.Min(20, (Height - 63) / 9));
        DrawGrid(12, $"Source • Move {value.SourceMove}", value.SourceReachable);
        DrawGrid(42 + cell * 9, $"Authored • Move {value.AuthoredMove}", value.AuthoredReachable);
        e.Graphics.DrawString($"Flat 9×9 • Base Jump {value.AuthoredJump} has no effect here • Center = unit", Font, Brushes.DimGray, 10, 36 + cell * 9);
        void DrawGrid(int left, string caption, IEnumerable<TilePosition> positions)
        {
            e.Graphics.DrawString(caption, Font, Brushes.Black, left, 3);
            var reachable = positions.ToHashSet();
            for (int y = 0; y < 9; y++) for (int x = 0; x < 9; x++)
            {
                var rectangle = new Rectangle(left + x * cell, 28 + y * cell, cell - 1, cell - 1);
                e.Graphics.FillRectangle(x == 4 && y == 4 ? Brushes.DarkSlateBlue : reachable.Contains(new(x, y, 0)) ? Brushes.MediumAquamarine : Brushes.Gainsboro, rectangle);
            }
        }
    }
}
