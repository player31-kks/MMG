using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MMG.Views.Test
{
    /// <summary>
    /// 드래그 중인 아이템의 시각적 복사본을 표시하는 Adorner
    /// </summary>
    public class DragAdorner : Adorner
    {
        private readonly VisualBrush _visualBrush;
        private readonly double _width;
        private readonly double _height;
        private Point _location;

        public DragAdorner(UIElement adornedElement, UIElement draggedElement, Point startPoint)
            : base(adornedElement)
        {
            _width = draggedElement.RenderSize.Width;
            _height = draggedElement.RenderSize.Height;

            _visualBrush = new VisualBrush(draggedElement)
            {
                Opacity = 0.7,
                Stretch = Stretch.None
            };

            _location = startPoint;
            IsHitTestVisible = false;
        }

        public void UpdatePosition(Point location)
        {
            _location = location;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            // 드래그 중인 아이템의 반투명 복사본 그리기
            var rect = new Rect(
                _location.X - _width / 2,
                _location.Y - _height / 2,
                _width,
                _height);

            // 그림자 효과
            var shadowRect = new Rect(
                rect.X + 3,
                rect.Y + 3,
                rect.Width,
                rect.Height);
            drawingContext.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                null,
                shadowRect,
                8, 8);

            // 메인 아이템
            drawingContext.DrawRoundedRectangle(
                _visualBrush,
                new Pen(new SolidColorBrush(Color.FromRgb(111, 95, 245)), 2),
                rect,
                8, 8);
        }
    }

    /// <summary>
    /// 드롭 위치를 나타내는 인디케이터 Adorner
    /// </summary>
    public class DropIndicatorAdorner : Adorner
    {
        private int _insertIndex = -1;
        private readonly ItemsControlAdornerInfo _info;

        public DropIndicatorAdorner(UIElement adornedElement, ItemsControlAdornerInfo info)
            : base(adornedElement)
        {
            _info = info;
            IsHitTestVisible = false;
        }

        public void UpdateInsertIndex(int index)
        {
            if (_insertIndex != index)
            {
                _insertIndex = index;
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (_insertIndex < 0 || _info.ItemPositions.Count == 0)
                return;

            double y;
            if (_insertIndex >= _info.ItemPositions.Count)
            {
                // 마지막 아이템 아래
                var lastPos = _info.ItemPositions[_info.ItemPositions.Count - 1];
                y = lastPos.Y + lastPos.Height + 3;
            }
            else
            {
                // 해당 아이템 위
                y = _info.ItemPositions[_insertIndex].Y - 3;
            }

            // 인디케이터 라인 그리기
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(111, 95, 245)), 3)
            {
                DashStyle = DashStyles.Solid
            };

            double leftMargin = 8;
            double rightMargin = 8;
            double width = AdornedElement.RenderSize.Width - leftMargin - rightMargin;

            // 라인
            drawingContext.DrawLine(pen, new Point(leftMargin, y), new Point(leftMargin + width, y));

            // 양쪽 끝 원
            var circleBrush = new SolidColorBrush(Color.FromRgb(111, 95, 245));
            drawingContext.DrawEllipse(circleBrush, null, new Point(leftMargin, y), 4, 4);
            drawingContext.DrawEllipse(circleBrush, null, new Point(leftMargin + width, y), 4, 4);
        }
    }

    /// <summary>
    /// ItemsControl의 아이템 위치 정보
    /// </summary>
    public class ItemsControlAdornerInfo
    {
        public List<Rect> ItemPositions { get; } = new List<Rect>();

        public void Clear() => ItemPositions.Clear();

        public void AddItemPosition(Rect rect) => ItemPositions.Add(rect);

        public int GetInsertIndex(Point point)
        {
            for (int i = 0; i < ItemPositions.Count; i++)
            {
                var rect = ItemPositions[i];
                // 아이템의 중간 지점보다 위에 있으면 그 위치에 삽입
                if (point.Y < rect.Y + rect.Height / 2)
                {
                    return i;
                }
            }
            // 모든 아이템 아래면 마지막에 삽입
            return ItemPositions.Count;
        }
    }
}
