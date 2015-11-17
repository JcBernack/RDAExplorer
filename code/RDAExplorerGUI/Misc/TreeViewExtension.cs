using AnnoModificationManager4.Controls;
using AnnoModificationManager4.Misc;
using System.Linq;
using System.Windows.Controls;

namespace RDAExplorerGUI.Misc
{
    public static class TreeViewExtension
    {
        public static TreeView GetTreeView(this TreeViewItem item)
        {
            var treeViewItem = item;
            while (!(treeViewItem.Parent is TreeView))
                treeViewItem = treeViewItem.Parent as TreeViewItem;
            return (TreeView) treeViewItem.Parent;
        }

        public static string GetNavigator(this ModifiedTreeViewItem item)
        {
            var str = item.SemanticValue;
            var parent = item.Parent as ModifiedTreeViewItem;
            if (parent != null)
                str = GetNavigator(parent) + "/" + str;
            return str.Trim('/');
        }

        public static ModifiedTreeViewItem NavigateTo(this TreeView view, string path, bool autocreate)
        {
            path = path.Replace("\\", "/");
            var list = path.Split('/').ToList();
            var message = list[0];
            foreach (ModifiedTreeViewItem view1 in view.Items)
            {
                if (view1.SemanticValue != message) continue;
                if (list.Count == 1)
                    return view1;
                list.RemoveAt(0);
                return NavigateTo(view1, StringExtension.PutTogether(list, '/'), autocreate);
            }
            if (!autocreate)
                return null;
            var view2 = new ModifiedTreeViewItem
            {
                Header = ControlExtension.BuildImageTextblock("pack://application:,,,/Images/Icons/folder.png", message),
                SemanticValue = message
            };
            view.Items.Add(view2);
            if (list.Count == 1)
                return view2;
            list.RemoveAt(0);
            return NavigateTo(view2, StringExtension.PutTogether(list, '/'), autocreate);
        }

        private static ModifiedTreeViewItem NavigateTo(ModifiedTreeViewItem view, string path, bool autocreate)
        {
            path = path.Replace("\\", "/");
            var list = path.Split('/').ToList();
            var message = list[0];
            foreach (ModifiedTreeViewItem view1 in view.Items)
            {
                if (view1.SemanticValue != message) continue;
                if (list.Count == 1)
                    return view1;
                list.RemoveAt(0);
                return NavigateTo(view1, StringExtension.PutTogether(list, '/'), autocreate);
            }
            if (!autocreate)
                return null;
            var view2 = new ModifiedTreeViewItem
            {
                Header = ControlExtension.BuildImageTextblock("pack://application:,,,/Images/Icons/folder.png", message),
                SemanticValue = message
            };
            view.Items.Add(view2);
            if (list.Count == 1)
                return view2;
            list.RemoveAt(0);
            return NavigateTo(view2, StringExtension.PutTogether(list, '/'), autocreate);
        }
    }
}
