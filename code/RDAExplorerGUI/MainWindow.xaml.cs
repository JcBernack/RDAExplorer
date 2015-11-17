using AnnoModificationManager4.Misc;
using AnnoModificationManager4.UserInterface.Misc;
using Microsoft.VisualBasic.FileIO;
using RDAExplorer;
using RDAExplorerGUI.Misc;
using RDAExplorerGUI.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace RDAExplorerGUI
{
    public partial class MainWindow
    {
        public RDAReader CurrentReader = new RDAReader();
        public string CurrentFileName = "";
        public List<RDAFile> FileWatcher_ToUpdate = new List<RDAFile>();
        public static MainWindow CurrentMainWindow;
        public FileSystemWatcher FileWatcher;
        public bool FileWatcher_Updating;
        private readonly FolderBrowserDialog _extractDialog;

        public MainWindow()
        {
            CurrentMainWindow = this;
            InitializeComponent();
            Width = Settings.Default.Window_Width;
            Height = Settings.Default.Window_Height;
            Left = Settings.Default.Window_X;
            Top = Settings.Default.Window_Y;
            WindowState = Settings.Default.Window_IsMaximized ? WindowState.Maximized : WindowState.Normal;
            _extractDialog = new FolderBrowserDialog();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NewFile();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                Settings.Default.Window_Width = Width;
                Settings.Default.Window_Height = Height;
                Settings.Default.Window_X = Left;
                Settings.Default.Window_Y = Top;
            }
            Settings.Default.Window_IsMaximized = WindowState == WindowState.Maximized;
            Settings.Default.Save();
            ResetDocument();
            try
            {
                Directory.Delete(DirectoryExtension.GetTempWorkingDirectory(), true);
            }
            catch (Exception)
            {
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            FileWatcher_ToUpdate = FileWatcher_ToUpdate.Distinct().ToList();
            if (FileWatcher_ToUpdate.Count == 0 || FileWatcher_Updating)
                return;
            var str1 = "Following files has changed:\n";
            foreach (var rdaFile in FileWatcher_ToUpdate)
                str1 = str1 + rdaFile.FileName + "\n";
            var message = str1 + "\nDo you want to update the RDA File Items?";
            FileWatcher_Updating = true;
            if (MessageWindow.Show(message, MessageWindow.MessageWindowType.YesNo) == MessageBoxResult.Yes)
            {
                foreach (RDAFile rdaFile in FileWatcher_ToUpdate)
                {
                    var str2 = DirectoryExtension.GetTempWorkingDirectory() + "\\" + rdaFile.FileName;
                    var str3 = StringExtension.MakeUnique(Path.ChangeExtension(str2, null) + "$", Path.GetExtension(str2), (File.Exists));
                    File.Copy(str2, str3);
                    rdaFile.SetFile(str3);
                }
            }
            FileWatcher_Updating = false;
            FileWatcher_ToUpdate.Clear();
        }

        public void RebuildTreeView()
        {
            BackgroundWorker wrk = new BackgroundWorker { WorkerReportsProgress = true };
            progressBar_Status.Visibility = Visibility.Visible;
            wrk.ProgressChanged += (s, e) => Application.Current.Dispatch(() =>
            {
                progressBar_Status.Value = e.ProgressPercentage;
                label_Status.Text = "Updating UI";
            });
            wrk.DoWork += (s, e) => _RebuildTreeView(wrk);
            wrk.RunWorkerCompleted += (s, e) => Application.Current.Dispatch(() => progressBar_Status.Visibility = Visibility.Collapsed);
            wrk.RunWorkerAsync();
        }

        private void _RebuildTreeView(BackgroundWorker wrk)
        {
            Application.Current.Dispatch(() =>
            {
                treeView.Items.Clear();

                RDAFolder root = CurrentReader.rdaFolder;
                foreach (RDAFolder folder in root.Folders)
                {
                    treeView.Items.Add(new RDAFolderTreeViewItem()
                    {
                        Folder = folder,
                        Header = ControlExtension.BuildImageTextblock("pack://application:,,,/Images/Icons/folder.png", folder.Name)
                    });
                }

                foreach (RDAFile file in root.Files)
                {
                    treeView.Items.Add(file.ToTreeViewItem());
                }

                foreach (RDASkippedDataSection skippedBlock in CurrentReader.SkippedDataSections)
                {
                    string title = skippedBlock.blockInfo.fileCount + " encrypted files";
                    treeView.Items.Add(new RDASkippedDataSectionTreeViewItem()
                    {
                        Section = skippedBlock,
                        Header = ControlExtension.BuildImageTextblock("pack://application:,,,/Images/Icons/error.png", title)
                    });
                }
            });
        }

        private void context_AddFiles_Click(object sender, RoutedEventArgs e)
        {
            AnnoModificationManager4.Misc.OpenFileDialog openFileDialog = new AnnoModificationManager4.Misc.OpenFileDialog();
            openFileDialog.Filter = "All files|*.*";
            openFileDialog.Multiselect = true;
            var nullable = openFileDialog.ShowDialog();
            if ((!nullable.GetValueOrDefault() ? 0 : (nullable.HasValue ? 1 : 0)) == 0)
                return;
            foreach (var file in openFileDialog.FileNames)
            {
                var generatedRDAFileName = RDAFile.FileNameToRDAFileName(file, CurrentReader.rdaFolder.FullPath);
                var rdafile = CurrentReader.rdaFolder.Files.Find(f => f.FileName == generatedRDAFileName);
                if (rdafile == null)
                {
                    var rdaFile = RDAFile.Create(CurrentReader.rdaFolder.Version, file, CurrentReader.rdaFolder.FullPath);
                    if (rdaFile != null)
                        CurrentReader.rdaFolder.Files.Add(rdaFile);
                }
                else
                    rdafile.SetFile(file, true);
            }
            RebuildTreeView();
        }

        private void context_AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            var files = new List<RDAFile>();
            foreach (var str in Directory.GetFiles(folderBrowserDialog.SelectedPath, "*", System.IO.SearchOption.AllDirectories))
            {
                var folderpath = (CurrentReader.rdaFolder.FullPath + "\\" + (Path.GetFileName(folderBrowserDialog.SelectedPath) + "\\" + Path.GetDirectoryName(str).Replace(folderBrowserDialog.SelectedPath, "")).Trim('\\')).Trim('\\');
                var rdaDestFile = RDAFile.FileNameToRDAFileName(str, folderpath);
                var rdafile = CurrentReader.rdaFolder.GetAllFiles().Find(f => f.FileName == rdaDestFile);
                if (rdafile == null)
                {
                    RDAFile rdaFile = RDAFile.Create(CurrentReader.rdaFolder.Version, str, folderpath);
                    if (rdaFile != null)
                        files.Add(rdaFile);
                }
                else
                    rdafile.SetFile(str, true);
            }
            CurrentReader.rdaFolder.AddFiles(files);
            RebuildTreeView();
        }

        private void context_AddFolderAsRoot_Click(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            var files = new List<RDAFile>();
            foreach (var str in Directory.GetFiles(folderBrowserDialog.SelectedPath, "*", System.IO.SearchOption.AllDirectories))
            {
                var folderpath = (CurrentReader.rdaFolder.FullPath + "\\" + Path.GetDirectoryName(str).Replace(folderBrowserDialog.SelectedPath, "")).Trim('\\');
                var rdaDestFile = RDAFile.FileNameToRDAFileName(str, folderpath);
                var rdafile = CurrentReader.rdaFolder.GetAllFiles().Find(f => f.FileName == rdaDestFile);
                if (rdafile == null)
                {
                    var rdaFile = RDAFile.Create(CurrentReader.rdaFolder.Version, str, folderpath);
                    if (rdaFile != null)
                        files.Add(rdaFile);
                }
                else
                    rdafile.SetFile(str, true);
            }
            CurrentReader.rdaFolder.AddFiles(files);
            RebuildTreeView();
        }

        private void context_NewFolder_Click(object sender, RoutedEventArgs e)
        {
            string text = MessageWindow.GetText("Folder name:", "New Folder");
            if (text == null)
                return;
            string filename = text.Replace(Path.GetInvalidPathChars(), "").Replace("\\", "").Replace("/", "");
            if (string.IsNullOrEmpty(filename))
                return;
            string str = StringExtension.MakeUnique(filename, "", f => CurrentReader.rdaFolder.Folders.Find(n => n.Name == f) != null);
            CurrentReader.rdaFolder.Folders.Add(new RDAFolder(CurrentReader.rdaFolder)
            {
                FullPath = "\\" + str,
                Name = str,
            });
            RebuildTreeView();
        }

        private void file_New_Click(object sender, RoutedEventArgs e)
        {
            NewFile();
        }

        private void file_OpenReadOnly_Click(object sender, RoutedEventArgs e)
        {
            AnnoModificationManager4.Misc.OpenFileDialog openFileDialog = new AnnoModificationManager4.Misc.OpenFileDialog();
            openFileDialog.Filter = "Valid Files|*.rda;*.sww;*.rdu|All files|*.*";
            bool? nullable = openFileDialog.ShowDialog();
            if ((!nullable.GetValueOrDefault() ? 0 : (nullable.HasValue ? 1 : 0)) == 0)
                return;
            OpenFile(openFileDialog.FileName, true);
        }

        private void file_Open_Click(object sender, RoutedEventArgs e)
        {
            AnnoModificationManager4.Misc.OpenFileDialog openFileDialog = new AnnoModificationManager4.Misc.OpenFileDialog();
            openFileDialog.Filter = "Valid Files|*.rda;*.sww;*.rdu|All files|*.*";
            bool? nullable = openFileDialog.ShowDialog();
            if ((!nullable.GetValueOrDefault() ? 0 : (nullable.HasValue ? 1 : 0)) == 0)
                return;
            OpenFile(openFileDialog.FileName, false);
        }

        private void file_Save_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentReader.rdaFolder.GetAllFiles().Count == 0)
            {
                MessageWindow.Show("Cannot save an empty file!");
            }
            else if (string.IsNullOrEmpty(CurrentFileName))
            {
                file_SaveAs_Click(null, null);
            }
            else
            {
                SaveFile(CurrentFileName);
            }
        }

        private void SaveFile(string fileName)
        {
            SaveRDAFileWindow saveRdaFileWindow = new SaveRDAFileWindow
            {
                Folder = CurrentReader.rdaFolder,
                field_OutputFile = { Text = fileName },
                MustChooseFolderVersionDueToEncryptedBlocks = CurrentReader.SkippedDataSections.Count > 0
            };
            if (!saveRdaFileWindow.ShowDialog().GetValueOrDefault())
                return;

            fileName = saveRdaFileWindow.field_OutputFile.Text;
            var version = CurrentReader.SkippedDataSections.Count > 0 ? CurrentReader.rdaFolder.Version : saveRdaFileWindow.SelectedVersion;
            var compress = saveRdaFileWindow.check_IsCompressed.IsChecked.Value;

            if (!Directory.Exists(Path.GetDirectoryName(fileName)))
                Directory.CreateDirectory(Path.GetDirectoryName(fileName));

            var writer = new RDAWriter(CurrentReader.rdaFolder);
            var wrk = new BackgroundWorker { WorkerReportsProgress = true };
            progressBar_Status.Visibility = Visibility.Visible;
            wrk.ProgressChanged += (s, e2) => Application.Current.Dispatch(() =>
            {
                label_Status.Text = writer.UI_LastMessage;
                progressBar_Status.Value = e2.ProgressPercentage;
            });
            wrk.RunWorkerCompleted += (s, e2) => Application.Current.Dispatch(() =>
            {
                label_Status.Text = CurrentReader.rdaFolder.GetAllFiles().Count + " files";
                progressBar_Status.Visibility = Visibility.Collapsed;
            });
            wrk.DoWork += (s, e2) =>
            {
                try
                {
                    writer.Write(fileName, version, compress, CurrentReader, wrk);
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatch(() => MessageWindow.Show(ex.Message));
                }
            };
            wrk.RunWorkerAsync();
        }

        private void file_SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentReader.rdaFolder.GetAllFiles().Count == 0)
            {
                MessageWindow.Show("Cannot save an empty file!");
            }
            else
            {
                AnnoModificationManager4.Misc.SaveFileDialog saveFileDialog = new AnnoModificationManager4.Misc.SaveFileDialog();
                saveFileDialog.Filter = "RDA File|*.rda|Savegame|*.sww|Scenario|*.rdu";
                if (!saveFileDialog.ShowDialog().GetValueOrDefault())
                    return;

                SaveFile(saveFileDialog.FileName);
            }
        }

        private void file_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void archive_ExtractAll_Click(object sender, RoutedEventArgs args)
        {
            if (_extractDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var wrk = new BackgroundWorker { WorkerReportsProgress = true };
            progressBar_Status.Visibility = Visibility.Visible;
            wrk.ProgressChanged += (s, e) => Application.Current.Dispatch(() =>
            {
                label_Status.Text = RDAExplorer.RDAFileExtension.ExtractAll_LastMessage;
                progressBar_Status.Value = e.ProgressPercentage;
            });
            wrk.RunWorkerCompleted += (s, e) => Application.Current.Dispatch(() =>
            {
                label_Status.Text = CurrentReader.rdaFolder.GetAllFiles().Count + " files";
                progressBar_Status.Visibility = Visibility.Collapsed;
            });
            wrk.DoWork += (s, e) =>
            {
                try
                {
                    CurrentReader.rdaFolder.GetAllFiles().ExtractAll(_extractDialog.SelectedPath, wrk);
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatch(() => MessageWindow.Show(ex.Message));
                }
            };
            wrk.RunWorkerAsync();
        }

        private void archive_ExtractSelected_Click(object sender, RoutedEventArgs args)
        {
            if (_extractDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var wrk = new BackgroundWorker { WorkerReportsProgress = true };
            progressBar_Status.Visibility = Visibility.Visible;
            wrk.ProgressChanged += (s, e) => Application.Current.Dispatch(() =>
            {
                label_Status.Text = RDAExplorer.RDAFileExtension.ExtractAll_LastMessage;
                progressBar_Status.Value = e.ProgressPercentage;
            });
            wrk.RunWorkerCompleted += (s, e) => Application.Current.Dispatch(() =>
            {
                label_Status.Text = CurrentReader.rdaFolder.GetAllFiles().Count + " files";
                progressBar_Status.Visibility = Visibility.Collapsed;
            });
            wrk.DoWork += (s, e) =>
            {
                try
                {
                    var list = new List<RDAFile>();
                    foreach (var fileTreeViewItem in treeView.SelectedItems.OfType<RDAFileTreeViewItem>())
                        list.Add(fileTreeViewItem.File);
                    foreach (var folderTreeViewItem in treeView.SelectedItems.OfType<RDAFolderTreeViewItem>())
                        list.AddRange(folderTreeViewItem.Folder.GetAllFiles());
                    list.Distinct().ToList().ExtractAll(_extractDialog.SelectedPath, wrk);
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatch(() => MessageWindow.Show(ex.Message));
                }
            };
            wrk.RunWorkerAsync();
        }

        private void archive_SearchFile_Click(object sender, RoutedEventArgs e)
        {
            var text = MessageWindow.GetText("Search File with Name", "File.ext");
            if (text == null) return;
            foreach (var folderTreeViewItem in treeView.Items.OfType<RDAFolderTreeViewItem>())
            {
                var fileTreeViewItem = folderTreeViewItem.SearchFile(text);
                if (fileTreeViewItem != null)
                {
                    fileTreeViewItem.IsSelected = true;
                    break;
                }
            }
        }

        private void archive_SearchFolder_Click(object sender, RoutedEventArgs e)
        {
            var text = MessageWindow.GetText("Search Folder with Name", "Folder");
            if (text == null) return;
            foreach (var folderTreeViewItem1 in treeView.Items.OfType<RDAFolderTreeViewItem>())
            {
                var folderTreeViewItem2 = folderTreeViewItem1.SearchFolder(text);
                if (folderTreeViewItem2 != null)
                {
                    folderTreeViewItem2.IsSelected = true;
                    break;
                }
            }
        }

        private void button_Filter_Refresh_Click(object sender, RoutedEventArgs e)
        {
            RebuildTreeView();
        }

        private void ResetDocument()
        {
            CurrentFileName = "";
            file_Save.IsEnabled = true;
            FileWatcher?.Dispose();
            FileWatcher = new FileSystemWatcher
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite
            };
            CurrentReader.Dispose();
            DirectoryExtension.CleanDirectory(DirectoryExtension.GetTempWorkingDirectory());
            FileWatcher.Path = DirectoryExtension.GetTempWorkingDirectory();
            FileWatcher.EnableRaisingEvents = true;
        }

        private void NewFile()
        {
            Title = GetTitle();
            label_Status.Text = "";
            ResetDocument();
            CurrentReader = new RDAReader();
            RebuildTreeView();
        }

        private void OpenFile(string fileName, bool isreadonly)
        {
            RDAReader reader = new RDAReader();
            ResetDocument();
            CurrentFileName = fileName;
            if (!isreadonly)
                fileName = DirectoryExtension.GetTempWorkingDirectory() + "\\" + Path.GetFileName(fileName);
            else
                file_Save.IsEnabled = false;
            CurrentReader = reader;
            reader.FileName = fileName;
            progressBar_Status.Visibility = Visibility.Visible;
            Title = GetTitle() + " - " + Path.GetFileName(reader.FileName);
            reader.backgroundWorker = new BackgroundWorker();
            reader.backgroundWorker.WorkerReportsProgress = true;
            reader.backgroundWorker.ProgressChanged += (sender2, e2) => Application.Current.Dispatch(() =>
            {
                progressBar_Status.Value = e2.ProgressPercentage;
                label_Status.Text = reader.backgroundWorkerLastMessage;
            });
            reader.backgroundWorker.DoWork += (sender2, e2) =>
            {
                try
                {
                    if (!isreadonly)
                    {
                        Application.Current.Dispatch(() => label_Status.Text = "Copying *.rda file to a temparary directory ...");
                        FileSystem.CopyFile(CurrentFileName, fileName, UIOption.AllDialogs, UICancelOption.ThrowException);
                    }
                    reader.ReadRDAFile();
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatch(() =>
                    {
                        MessageWindow.Show(ex.Message);
                        NewFile();
                    });
                }
            };
            reader.backgroundWorker.RunWorkerCompleted += (sender2, e2) =>
            {
                progressBar_Status.Visibility = Visibility.Collapsed;
                RebuildTreeView();
            };
            reader.backgroundWorker.RunWorkerAsync();
        }

        public string GetTitle()
        {
            return "Anno 1404/2070/2205 RDA Explorer Version " + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
    }
}
