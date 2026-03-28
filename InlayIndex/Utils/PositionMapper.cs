using Microsoft.VisualStudio.Text;
using System;

namespace InlayIndex.Utils
{
    public static class PositionMapper
    {
        public static SnapshotPoint MapToSnapshot(ITextSnapshot snapshot, int position)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (position < 0)
            {
                position = 0;
            }

            if (position > snapshot.Length)
            {
                position = snapshot.Length;
            }

            return new SnapshotPoint(snapshot, position);
        }

        public static SnapshotSpan CreateSpan(ITextSnapshot snapshot, int start, int length)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (start < 0)
            {
                start = 0;
            }

            if (start + length > snapshot.Length)
            {
                length = snapshot.Length - start;
            }

            if (length < 0)
            {
                length = 0;
            }

            return new SnapshotSpan(snapshot, start, length);
        }

        public static bool IsValidPosition(ITextSnapshot snapshot, int position)
        {
            return snapshot != null && position >= 0 && position <= snapshot.Length;
        }

        public static int ClampPosition(ITextSnapshot snapshot, int position)
        {
            if (snapshot == null)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(position, snapshot.Length));
        }
    }
}
