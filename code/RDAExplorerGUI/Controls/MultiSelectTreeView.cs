using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RDAExplorerGUI.Controls
{
    public class MultiSelectTreeView : TreeView
    {
        public static readonly DependencyProperty AutoRecursiveProperty = DependencyProperty.Register("AutoRecursive", typeof(bool), typeof(MultiSelectTreeView), new UIPropertyMetadata(false));
        public List<object> SelectedItems = new List<object>();

        public bool AutoRecursive
        {
            get
            {
                return (bool)GetValue(AutoRecursiveProperty);
            }
            set
            {
                SetValue(AutoRecursiveProperty, value);
            }
        }

        public List<object> AllItems
        {
            get
            {
                var list = new List<object>();
                foreach (var obj in Items)
                    list.AddRange(GetRecursiveItems(obj));
                return list;
            }
        }

        public MultiSelectTreeView()
        {
            SelectedItemChanged += MultiSelectTreeView_SelectedItemChanged;
        }

        private static IEnumerable<object> GetRecursiveItems(object item)
        {
            var list = new List<object> { item };
            var viewItem = item as TreeViewItem;
            if (viewItem == null) return list;
            foreach (var obj in viewItem.Items)
                list.AddRange(GetRecursiveItems(obj));
            return list;
        }

        public void UpdateSelectedItems()
        {
            foreach (TreeViewItem obj in AllItems.OfType<TreeViewItem>())
            {
                obj.Style = SelectedItems.Contains(obj) ? Application.Current.Resources["TreeViewItemStyle_Selected"] as Style : null;
            }
        }

        public void SelectItem(object item)
        {
            if (!SelectedItems.Contains(item))
                SelectedItems.Add(item);
            if (!(item is TreeViewItem) || !AutoRecursive)
                return;
            foreach (var obj in ((TreeViewItem) item).Items)
                SelectItem(obj);
        }

        private void MultiSelectTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (SelectedItem != null)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    SelectItem(SelectedItem);
                }
                else
                {
                    SelectedItems.Clear();
                    SelectItem(SelectedItem);
                }
            }
            else
                SelectedItems.Clear();
            UpdateSelectedItems();
        }
    }
}
