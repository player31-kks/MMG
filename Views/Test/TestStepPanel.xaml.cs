using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MMG.Models;
using MMG.ViewModels;

namespace MMG.Views.Test
{
    public partial class TestStepPanel : UserControl
    {
        private TestLogWindow? _logWindow;
        private Point _startPoint;
        private bool _isDragging;
        private TestStep? _draggedStep;
        
        // Adorner 관련 필드
        private DragAdorner? _dragAdorner;
        private DropIndicatorAdorner? _dropIndicatorAdorner;
        private AdornerLayer? _adornerLayer;
        private ItemsControlAdornerInfo _adornerInfo = new();
        private int _draggedIndex = -1;

        public TestStepPanel()
        {
            InitializeComponent();
        }

        private void OpenLogWindow_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TestsViewModel viewModel)
            {
                // 이미 창이 열려있으면 포커스
                if (_logWindow != null && _logWindow.IsLoaded)
                {
                    _logWindow.Activate();
                    return;
                }

                // 새 로그 창 열기
                _logWindow = new TestLogWindow(viewModel.LogItems);
                _logWindow.Owner = Window.GetWindow(this);
                _logWindow.Show();
            }
        }

        private void StepsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
            _draggedStep = null;
            _draggedIndex = -1;
            
            // 드래그 핸들(⋮⋮)을 클릭한 경우에만 드래그 시작 준비
            if (e.OriginalSource is FrameworkElement element)
            {
                var dragHandle = FindParentWithCursor(element, Cursors.SizeAll);
                if (dragHandle != null)
                {
                    var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                    if (listBoxItem != null)
                    {
                        _draggedStep = listBoxItem.DataContext as TestStep;
                        _isDragging = true;
                        
                        // 드래그 시작 시 아이템 위치 정보 수집
                        CollectItemPositions();
                        
                        // 드래그되는 아이템의 인덱스 저장
                        _draggedIndex = StepsListBox.Items.IndexOf(_draggedStep);
                    }
                }
            }
        }

        private FrameworkElement? FindParentWithCursor(FrameworkElement element, Cursor cursor)
        {
            var current = element;
            while (current != null)
            {
                if (current.Cursor == cursor)
                    return current;
                current = current.Parent as FrameworkElement;
            }
            return null;
        }

        private void StepsListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !_isDragging || _draggedStep == null)
            {
                return;
            }

            Point mousePos = e.GetPosition(null);
            Vector diff = _startPoint - mousePos;

            // 최소 드래그 거리 체크
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var listBox = sender as ListBox;
                if (listBox == null) return;

                // Adorner 생성
                CreateDragAdorner(e);
                CreateDropIndicatorAdorner();

                // 드래그 시작
                DataObject dragData = new DataObject("TestStep", _draggedStep);
                DragDrop.DoDragDrop(listBox, dragData, DragDropEffects.Move);
                
                // 드래그 종료 시 Adorner 제거
                RemoveAdorners();
                
                _isDragging = false;
                _draggedStep = null;
                _draggedIndex = -1;
            }
        }

        private void StepsListBox_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("TestStep"))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.Move;
            
            // Adorner 위치 업데이트
            Point position = e.GetPosition(StepsListBox);
            
            // DragAdorner 위치 업데이트
            if (_dragAdorner != null && _adornerLayer != null)
            {
                _dragAdorner.UpdatePosition(position);
            }
            
            // DropIndicator 위치 업데이트
            if (_dropIndicatorAdorner != null)
            {
                int insertIndex = _adornerInfo.GetInsertIndex(position);
                
                // 자기 자신 위치는 건너뛰기
                if (_draggedIndex >= 0)
                {
                    if (insertIndex == _draggedIndex || insertIndex == _draggedIndex + 1)
                    {
                        insertIndex = -1; // 인디케이터 숨기기
                    }
                }
                
                _dropIndicatorAdorner.UpdateInsertIndex(insertIndex);
            }
            
            e.Handled = true;
        }

        private void StepsListBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("TestStep"))
                return;

            var droppedStep = e.Data.GetData("TestStep") as TestStep;
            if (droppedStep == null) return;

            var listBox = sender as ListBox;
            if (listBox == null) return;

            // 드롭 위치 계산
            Point position = e.GetPosition(StepsListBox);
            int insertIndex = _adornerInfo.GetInsertIndex(position);

            // ViewModel에서 순서 변경 처리 (insertIndex 직접 전달)
            if (DataContext is TestsViewModel viewModel)
            {
                viewModel.ReorderStepToIndex(droppedStep, insertIndex);
            }

            e.Handled = true;
        }

        private void CollectItemPositions()
        {
            _adornerInfo.Clear();
            
            for (int i = 0; i < StepsListBox.Items.Count; i++)
            {
                var container = StepsListBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (container != null)
                {
                    var transform = container.TransformToAncestor(StepsListBox);
                    var topLeft = transform.Transform(new Point(0, 0));
                    var rect = new Rect(topLeft, container.RenderSize);
                    _adornerInfo.AddItemPosition(rect);
                }
            }
        }

        private void CreateDragAdorner(MouseEventArgs e)
        {
            if (_draggedStep == null) return;
            
            var listBoxItem = StepsListBox.ItemContainerGenerator.ContainerFromItem(_draggedStep) as ListBoxItem;
            if (listBoxItem == null) return;

            _adornerLayer = AdornerLayer.GetAdornerLayer(StepsListBox);
            if (_adornerLayer == null) return;

            Point position = e.GetPosition(StepsListBox);
            _dragAdorner = new DragAdorner(StepsListBox, listBoxItem, position);
            _adornerLayer.Add(_dragAdorner);
        }

        private void CreateDropIndicatorAdorner()
        {
            if (_adornerLayer == null)
            {
                _adornerLayer = AdornerLayer.GetAdornerLayer(StepsListBox);
            }
            
            if (_adornerLayer == null) return;

            _dropIndicatorAdorner = new DropIndicatorAdorner(StepsListBox, _adornerInfo);
            _adornerLayer.Add(_dropIndicatorAdorner);
        }

        private void RemoveAdorners()
        {
            if (_adornerLayer != null)
            {
                if (_dragAdorner != null)
                {
                    _adornerLayer.Remove(_dragAdorner);
                    _dragAdorner = null;
                }
                
                if (_dropIndicatorAdorner != null)
                {
                    _adornerLayer.Remove(_dropIndicatorAdorner);
                    _dropIndicatorAdorner = null;
                }
            }
            
            _adornerLayer = null;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T item)
                    return item;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}