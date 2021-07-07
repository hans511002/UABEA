using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UABEAvalonia.Plugins;

namespace UABEAvalonia
{
    public class InfoWindow : Window
    {
        //controls
        private MenuItem menuAdd;
        private MenuItem menuSave;
        private MenuItem menuCreateStandaloneInstaller;
        private MenuItem menuCreatePackageFile;
        private MenuItem menuClose;
        private MenuItem menuSearchByName;
        private MenuItem menuContinueSearch;
        private MenuItem menuGoToAsset;
        private MenuItem menuDependencies;
        private MenuItem menuContainers;
        private Button btnViewData;
        private Button btnExportRaw;
        private Button btnExportDump;
        private Button btnPlugin;
        private Button btnImportRaw;
        private Button btnImportDump;
        private Button btnRemove;
        private DataGrid dataGrid;
        private TextBox boxName;
        private TextBox boxPathId;
        private TextBox boxFileId;
        private TextBox boxType;

        //todo, rework all this
        public AssetWorkspace Workspace { get; }
        public AssetsManager am { get => Workspace.am; }

        //searching
        private string searchText;
        private int searchStart;
        private bool searchDown;
        private bool searchCaseSensitive;
        private bool searching;

        //would prefer using a stream over byte[] but whatever, will for now
        public List<Tuple<AssetsFileInstance, byte[]>> ChangedAssetsDatas { get; set; }

        private ObservableCollection<AssetInfoDataGridItem> dataGridItems;

        private PluginManager pluginManager;

        //for preview
        public InfoWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated items
            menuAdd = this.FindControl<MenuItem>("menuAdd");
            menuSave = this.FindControl<MenuItem>("menuSave");
            menuCreateStandaloneInstaller = this.FindControl<MenuItem>("menuCreateStandaloneInstaller");
            menuCreatePackageFile = this.FindControl<MenuItem>("menuCreatePackageFile");
            menuClose = this.FindControl<MenuItem>("menuClose");
            menuSearchByName = this.FindControl<MenuItem>("menuSearchByName");
            menuContinueSearch = this.FindControl<MenuItem>("menuContinueSearch");
            menuGoToAsset = this.FindControl<MenuItem>("menuGoToAsset");
            menuDependencies = this.FindControl<MenuItem>("menuDependencies");
            menuContainers = this.FindControl<MenuItem>("menuContainers");
            btnViewData = this.FindControl<Button>("btnViewData");
            btnExportRaw = this.FindControl<Button>("btnExportRaw");
            btnExportDump = this.FindControl<Button>("btnExportDump");
            btnPlugin = this.FindControl<Button>("btnPlugin");
            btnImportRaw = this.FindControl<Button>("btnImportRaw");
            btnImportDump = this.FindControl<Button>("btnImportDump");
            btnRemove = this.FindControl<Button>("btnRemove");
            dataGrid = this.FindControl<DataGrid>("dataGrid");
            boxName = this.FindControl<TextBox>("boxName");
            boxPathId = this.FindControl<TextBox>("boxPathId");
            boxFileId = this.FindControl<TextBox>("boxFileId");
            boxType = this.FindControl<TextBox>("boxType");
            //generated events
            KeyDown += InfoWindow_KeyDown;
            menuAdd.Click += MenuAdd_Click;
            menuSave.Click += MenuSave_Click;
            menuCreatePackageFile.Click += MenuCreatePackageFile_Click;
            menuClose.Click += MenuClose_Click;
            menuSearchByName.Click += MenuSearchByName_Click;
            menuContinueSearch.Click += MenuContinueSearch_Click;
            btnViewData.Click += BtnViewData_Click;
            btnExportRaw.Click += BtnExportRaw_Click;
            btnExportDump.Click += BtnExportDump_Click;
            btnImportRaw.Click += BtnImportRaw_Click;
            btnImportDump.Click += BtnImportDump_Click;
            btnRemove.Click += BtnRemove_Click;
            btnPlugin.Click += BtnPlugin_Click;
            dataGrid.SelectionChanged += DataGrid_SelectionChanged;
        }

        private void InfoWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.F3)
            {
                NextSearch();
            }else if (e.Key==Key.F && e.KeyModifiers== KeyModifiers.Control)
            {
                MenuSearchByName_Click(sender,null);
            }
        }

        public InfoWindow(AssetsManager assetsManager, List<AssetsFileInstance> assetsFiles, bool fromBundle) : this()
        {
            Workspace = new AssetWorkspace(assetsManager, fromBundle);
            Workspace.ItemUpdated += Workspace_ItemUpdated;

            LoadAllAssetsWithDeps(assetsFiles);
            MakeDataGridItems();
            dataGrid.Items = dataGridItems;

            pluginManager = new PluginManager();
            pluginManager.LoadPluginsInDirectory("plugins");

            searchText = "";
            searchStart = 0;
            searchDown = false;
            searchCaseSensitive = true;
            searching = false;

            ChangedAssetsDatas = new List<Tuple<AssetsFileInstance, byte[]>>();
        }

        private async void MenuAdd_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            AddAssetWindow win = new AddAssetWindow(Workspace);
            await win.ShowDialog(this);
        }

        private async void MenuSave_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveFile();
            ClearModified();
            Workspace.Modified = false;
        }

        private async void MenuCreatePackageFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ModMakerDialog dialog = new ModMakerDialog(Workspace);
            await dialog.ShowDialog(this);
        }

        private async void MenuClose_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (Workspace.Modified)
            {
                ButtonResult choice = await MessageBoxUtil.ShowDialog(this,
                    "Changes made", "You've modified this file. Would you like to save?",
                    ButtonEnum.YesNo);
                if (choice == ButtonResult.Yes)
                {
                    await SaveFile();
                }
            }
            CloseFile();
        }

        private async void MenuSearchByName_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            SearchDialog dialog = new SearchDialog();
            SearchDialogResult res = await dialog.ShowDialog<SearchDialogResult>(this);
            if (res != null && res.ok)
            {
                int selectedIndex = dataGrid.SelectedIndex;

                searchText = res.text;
                searchStart = selectedIndex != -1 ? selectedIndex : 0;
                searchDown = res.isDown;
                searchCaseSensitive = res.caseSensitive;
                searching = true;
                NextSearch();
            }
        }

        private void MenuContinueSearch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            NextSearch();
        }

        private async void BtnViewData_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            AssetInfoDataGridItem gridItem = GetSelectedGridItem();
            if (gridItem.Size > 500000)
            {
                var bigFileBox = MessageBoxManager
                    .GetMessageBoxCustomWindow(new MessageBoxCustomParams
                    {
                        Style = Style.Windows,
                        ContentHeader = "Warning",
                        ContentMessage = "The asset you are about to open is very big and may take a lot of time and memory.",
                        ButtonDefinitions = new[] {
                            new ButtonDefinition {Name = "Continue anyway", Type = ButtonType.Colored},
                            new ButtonDefinition {Name = "Cancel", Type = ButtonType.Default}
                        }
                    });
                string result = await bigFileBox.Show();
                if (result == "Cancel")
                    return;
            }

            List<AssetContainer> selectedConts = GetSelectedAssetsReplaced();
            if (selectedConts.Count > 0)
            {
                DataWindow data = new DataWindow(Workspace, selectedConts[0]);
                data.Show();
            }
        }

        private async void BtnExportRaw_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetContainer> selection = GetSelectedAssetsReplaced();

            if (selection.Count > 1)
                await BatchExportRaw(selection);
            else
                await SingleExportRaw(selection);
        }

        private async void BtnExportDump_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetContainer> selection = GetSelectedAssetsReplaced();

            if (selection.Count > 1)
                await BatchExportDump(selection);
            else
                await SingleExportDump(selection);
        }

        private async void BtnImportRaw_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetContainer> selection = GetSelectedAssetsReplaced();

            if (selection.Count > 1)
                await BatchImportRaw(selection);
            else
                await SingleImportRaw(selection);
        }

        private async void BtnImportDump_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetContainer> selection = GetSelectedAssetsReplaced();

            if (selection.Count > 1)
                await BatchImportDump(selection);
            else
                await SingleImportDump(selection);
        }

        private async void BtnRemove_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            ButtonResult choice = await MessageBoxUtil.ShowDialog(this,
                "Removing assets", "Removing an asset referenced by other assets can cause crashes!\nAre you sure?",
                ButtonEnum.YesNo);
            if (choice == ButtonResult.Yes)
            {
                List<AssetContainer> selection = GetSelectedAssetsReplaced();
                foreach (AssetContainer cont in selection)
                {
                    Workspace.AddReplacer(cont.FileInstance, new AssetsRemover(0, cont.PathId, (int)cont.ClassId));
                }
            }
        }

        private async void BtnPlugin_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetContainer> conts = GetSelectedAssetsReplaced();
            PluginWindow plug = new PluginWindow(this, Workspace, conts, pluginManager);
            await plug.ShowDialog(this);
        }

        private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var gridItem = (AssetInfoDataGridItem)dataGrid.SelectedItem;
            boxName.Text = gridItem.Name;
            boxPathId.Text = gridItem.PathID.ToString();
            boxFileId.Text = gridItem.FileID.ToString();
            boxType.Text = $"0x{gridItem.TypeID:X8} ({gridItem.Type})";
        }

        private async Task SaveFile()
        {
            var fileToReplacer = new Dictionary<AssetsFileInstance, List<AssetsReplacer>>();

            foreach (var newAsset in Workspace.NewAssets)
            {
                AssetID assetId = newAsset.Key;
                AssetsReplacer replacer = newAsset.Value;
                string fileName = assetId.fileName;

                if (Workspace.LoadedFileLookup.TryGetValue(fileName.ToLower(), out AssetsFileInstance? file))
                {
                    if (!fileToReplacer.ContainsKey(file))
                        fileToReplacer[file] = new List<AssetsReplacer>();

                    fileToReplacer[file].Add(replacer);
                }
            }

            if (Workspace.fromBundle)
            {
                ChangedAssetsDatas.Clear();
                foreach (var kvp in fileToReplacer)
                {
                    AssetsFileInstance file = kvp.Key;
                    List<AssetsReplacer> replacers = kvp.Value;

                    using (MemoryStream ms = new MemoryStream())
                    using (AssetsFileWriter w = new AssetsFileWriter(ms))
                    {
                        file.file.Write(w, 0, replacers, 0);
                        ChangedAssetsDatas.Add(new Tuple<AssetsFileInstance, byte[]>(file, ms.ToArray()));
                    }
                }
            }
            else
            {
                foreach (var kvp in fileToReplacer)
                {
                    AssetsFileInstance file = kvp.Key;
                    List<AssetsReplacer> replacers = kvp.Value;

                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.Title = "Save as...";
                    sfd.InitialFileName = file.name;

                    string filePath;

                    while (true)
                    {
                        filePath = await sfd.ShowAsync(this);

                        if (filePath == "" || filePath == null)
                            continue;

                        if (Path.GetFullPath(filePath) == Path.GetFullPath(file.path))
                        {
                            await MessageBoxUtil.ShowDialog(this,
                                "File in use", "Since this file is already open in UABEA, you must pick a new file name (sorry!)");
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }

                    try
                    {
                        using (FileStream fs = File.OpenWrite(filePath))
                        using (AssetsFileWriter w = new AssetsFileWriter(fs))
                        {
                            file.file.Write(w, 0, replacers, 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxUtil.ShowDialog(this,
                            "Write exception", "There was a problem while writing the file:\n" + ex.Message);
                    }
                }
            }
        }

        private void CloseFile()
        {
            am.UnloadAllAssetsFiles(true);
            Close();
        }

        private async Task BatchExportRaw(List<AssetContainer> selection)
        {
            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select export directory";

            string dir = await ofd.ShowAsync(this);

            if (dir != null && dir != string.Empty)
            {
                foreach (AssetContainer selectedCont in selection)
                {
                    AssetsFileInstance selectedInst = selectedCont.FileInstance;

                    Extensions.GetUABENameFast(selectedCont, am.classFile, out string assetName, out string _);
                    string file = Path.Combine(dir, $"{assetName}-{Path.GetFileName(selectedInst.path)}-{selectedCont.PathId}.dat");

                    using (FileStream fs = File.OpenWrite(file))
                    {
                        AssetImportExport dumper = new AssetImportExport();
                        dumper.DumpRawAsset(fs, selectedCont.FileReader, selectedCont.FilePosition, selectedCont.Size);
                    }
                }
            }
        }
        
        private async Task SingleExportRaw(List<AssetContainer> selection)
        {
            AssetContainer selectedCont = selection[0];
            AssetsFileInstance selectedInst = selectedCont.FileInstance;

            Extensions.GetUABENameFast(selectedCont, am.classFile, out string assetName, out string _);

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save As";
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "Raw Unity Asset", Extensions = new List<string>() { "dat" } }
            };
            sfd.InitialFileName = $"{assetName}-{Path.GetFileName(selectedInst.path)}-{selectedCont.PathId}.dat";

            string file = await sfd.ShowAsync(this);

            if (file != null && file != string.Empty)
            {
                using (FileStream fs = File.OpenWrite(file))
                {
                    AssetImportExport dumper = new AssetImportExport();
                    dumper.DumpRawAsset(fs, selectedCont.FileReader, selectedCont.FilePosition, selectedCont.Size);
                }
            }
        }

        private async Task BatchExportDump(List<AssetContainer> selection)
        {
            //todo: ask for txt or json

            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select export directory";

            string dir = await ofd.ShowAsync(this);

            if (dir != null && dir != string.Empty)
            {
                foreach (AssetContainer selectedCont in selection)
                {
                    Extensions.GetUABENameFast(selectedCont, am.classFile, out string assetName, out string _);
                    string file = Path.Combine(dir, $"{assetName}-{Path.GetFileName(selectedCont.FileInstance.path)}-{selectedCont.PathId}.txt");

                    using (FileStream fs = File.OpenWrite(file))
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        AssetTypeValueField baseField = Workspace.GetBaseField(selectedCont);

                        AssetImportExport dumper = new AssetImportExport();
                        dumper.DumpTextAsset(sw, baseField);
                    }
                }
            }
        }

        private async Task SingleExportDump(List<AssetContainer> selection)
        {
            AssetContainer selectedCont = selection[0];
            AssetsFileInstance selectedInst = selectedCont.FileInstance;

            Extensions.GetUABENameFast(selectedCont, am.classFile, out string assetName, out string _);

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save As";
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "UABE text dump", Extensions = new List<string>() { "txt" } },
                new FileDialogFilter() { Name = "UABE json dump", Extensions = new List<string>() { "json" } }
            };
            sfd.InitialFileName = $"{assetName}-{Path.GetFileName(selectedInst.path)}-{selectedCont.PathId}.txt";

            string file = await sfd.ShowAsync(this);

            if (file != null && file != string.Empty)
            {
                if (file.EndsWith(".json"))
                {
                    await MessageBoxUtil.ShowDialog(this, "Not implemented", "There's no json dump support yet, sorry. Exporting as .txt anyway.");
                    file = file.Substring(0, file.Length - 5) + ".txt";
                }

                using (FileStream fs = File.OpenWrite(file))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    AssetTypeValueField baseField = Workspace.GetBaseField(selectedCont);

                    AssetImportExport dumper = new AssetImportExport();
                    dumper.DumpTextAsset(sw, baseField);
                }
            }
        }

        private async Task BatchImportRaw(List<AssetContainer> selection)
        {
            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select import directory";

            string dir = await ofd.ShowAsync(this);

            if (dir != null && dir != string.Empty)
            {
                ImportBatch dialog = new ImportBatch(Workspace, selection, dir, ".dat");
                List<ImportBatchInfo> batchInfos = await dialog.ShowDialog<List<ImportBatchInfo>>(this);
                if (batchInfos != null)
                {
                    foreach (ImportBatchInfo batchInfo in batchInfos)
                    {
                        string selectedFilePath = batchInfo.importFile;
                        AssetContainer selectedCont = batchInfo.cont;
                        AssetsFileInstance selectedInst = selectedCont.FileInstance;

                        using (FileStream fs = File.OpenRead(selectedFilePath))
                        {
                            AssetImportExport importer = new AssetImportExport();
                            byte[] bytes = importer.ImportRawAsset(fs);

                            AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(selectedCont, bytes);
                            Workspace.AddReplacer(selectedInst, replacer, new MemoryStream(bytes));
                        }
                    }
                }
            }
        }

        private async Task SingleImportRaw(List<AssetContainer> selection)
        {
            AssetContainer selectedCont = selection[0];
            AssetsFileInstance selectedInst = selectedCont.FileInstance;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Open";
            ofd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "Raw Unity Asset", Extensions = new List<string>() { "dat" } }
            };

            string[] fileList = await ofd.ShowAsync(this);
            if (fileList.Length == 0)
                return;

            string file = fileList[0];

            if (file != null && file != string.Empty)
            {
                using (FileStream fs = File.OpenRead(file))
                {
                    AssetImportExport importer = new AssetImportExport();
                    byte[] bytes = importer.ImportRawAsset(fs);

                    AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(selectedCont, bytes);
                    Workspace.AddReplacer(selectedInst, replacer, new MemoryStream(bytes));
                }
            }
        }

        private async Task BatchImportDump(List<AssetContainer> selection)
        {
            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select import directory";

            string dir = await ofd.ShowAsync(this);

            if (dir != null && dir != string.Empty)
            {
                ImportBatch dialog = new ImportBatch(Workspace, selection, dir, ".txt");
                List<ImportBatchInfo> batchInfos = await dialog.ShowDialog<List<ImportBatchInfo>>(this);
                if (batchInfos != null)
                {
                    foreach (ImportBatchInfo batchInfo in batchInfos)
                    {
                        string selectedFilePath = batchInfo.importFile;
                        AssetContainer selectedCont = batchInfo.cont;
                        AssetsFileInstance selectedInst = selectedCont.FileInstance;

                        using (FileStream fs = File.OpenRead(selectedFilePath))
                        using (StreamReader sr = new StreamReader(fs))
                        {
                            AssetImportExport importer = new AssetImportExport();
                            byte[]? bytes = importer.ImportTextAsset(sr);

                            if (bytes == null)
                            {
                                await MessageBoxUtil.ShowDialog(this, "Parse error", "Something went wrong when reading the dump file.");
                                return;
                            }

                            AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(selectedCont, bytes);
                            Workspace.AddReplacer(selectedInst, replacer, new MemoryStream(bytes));
                        }
                    }
                }
            }
        }

        private async Task SingleImportDump(List<AssetContainer> selection)
        {
            AssetContainer selectedCont = selection[0];
            AssetsFileInstance selectedInst = selectedCont.FileInstance;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Open";
            ofd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "UABE text dump", Extensions = new List<string>() { "txt" } },
                new FileDialogFilter() { Name = "UABE json dump", Extensions = new List<string>() { "json" } }
            };

            string[] fileList = await ofd.ShowAsync(this);
            if (fileList.Length == 0)
                return;

            string file = fileList[0];

            if (file != null && file != string.Empty)
            {
                if (file.EndsWith(".json"))
                {
                    await MessageBoxUtil.ShowDialog(this, "Not implemented", "There's no json dump support yet, sorry.");
                    return;
                }

                using (FileStream fs = File.OpenRead(file))
                using (StreamReader sr = new StreamReader(fs))
                {
                    AssetImportExport importer = new AssetImportExport();
                    byte[]? bytes = importer.ImportTextAsset(sr);

                    if (bytes == null)
                    {
                        await MessageBoxUtil.ShowDialog(this, "Parse error", "Something went wrong when reading the dump file.");
                        return;
                    }

                    AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(selectedCont, bytes);
                    Workspace.AddReplacer(selectedInst, replacer, new MemoryStream(bytes));
                }
            }
        }

        private async void NextSearch()
        {
            bool foundResult = false;
            int _searchStart = searchStart;
            if (searching)
            {
                List<AssetInfoDataGridItem> itemList = dataGrid.Items.Cast<AssetInfoDataGridItem>().ToList();
                if (searchDown)
                {
                    for (int i = searchStart; i < itemList.Count; i++)
                    {
                        string name = itemList[i].Name;
                        if (Extensions.WildcardMatches(name, searchText, searchCaseSensitive))
                        {
                            dataGrid.SelectedIndex = i;
                            dataGrid.ScrollIntoView(dataGrid.SelectedItem, null);
                            searchStart = i + 1;
                            foundResult = true;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = searchStart; i >= 0; i--)
                    {
                        string name = itemList[i].Name;
                        if (Extensions.WildcardMatches(name, searchText, searchCaseSensitive))
                        {
                            dataGrid.SelectedIndex = i;
                            dataGrid.ScrollIntoView(dataGrid.SelectedItem, null);
                            searchStart = i - 1;
                            foundResult = true;
                            break;
                        }
                    }
                }
            }

            if (!foundResult)
            {
                if (_searchStart > 0)
                {
                    searchStart = 0;
                    NextSearch();
                }
                else
                {
                    await MessageBoxUtil.ShowDialog(this, "Search end", "Can't find any assets that match.");
                    //searchText = "";
                    searchStart = 0;
                    //searchDown = false;
                    //searching = false;
                }
                return;
            }
        }

        private ObservableCollection<AssetInfoDataGridItem> MakeDataGridItems()
        {
            dataGridItems = new ObservableCollection<AssetInfoDataGridItem>();

            Workspace.GenerateAssetsFileLookup();

            foreach (AssetContainer cont in Workspace.LoadedAssets.Values)
            {
                AddDataGridItem(cont);
            }
            return dataGridItems;
        }

        private AssetInfoDataGridItem AddDataGridItem(AssetContainer cont, bool isNewAsset = false)
        {
            AssetsFileInstance thisFileInst = cont.FileInstance;
            AssetsFile thisFile = thisFileInst.file;

            string name;
            string container;
            string type;
            int fileId;
            long pathId;
            int size;
            string modified;

            container = string.Empty;
            fileId = Workspace.LoadedFiles.IndexOf(thisFileInst); //todo faster lookup THIS IS JUST FOR TESTING
            pathId = cont.PathId;
            size = (int)cont.Size;
            modified = "";

            Extensions.GetUABENameFast(thisFile, am.classFile, cont.FileReader, cont.FilePosition, cont.ClassId, cont.MonoId, out name, out type);

            var item = new AssetInfoDataGridItem
            {
                Name = name,
                Container = container,
                Type = type,
                TypeID = cont.ClassId,
                FileID = fileId,
                PathID = pathId,
                Size = size,
                Modified = modified,
                assetContainer = cont
            };

            if (!isNewAsset)
                dataGridItems.Add(item);
            else
                dataGridItems.Insert(0, item);
            return item;
        }

        private void LoadAllAssetsWithDeps(List<AssetsFileInstance> files)
        {
            HashSet<string> fileNames = new HashSet<string>();
            foreach (AssetsFileInstance file in files)
            {
                RecurseGetAllAssets(file, Workspace.LoadedAssets, Workspace.LoadedFiles, fileNames);
            }
        }

        private void RecurseGetAllAssets(AssetsFileInstance fromFile, Dictionary<AssetID, AssetContainer> conts, List<AssetsFileInstance> files, HashSet<string> fileNames)
        {
            fromFile.table.GenerateQuickLookupTree();

            files.Add(fromFile);
            fileNames.Add(fromFile.path.ToLower());

            foreach (AssetFileInfoEx info in fromFile.table.assetFileInfo)
            {
                AssetContainer cont = new AssetContainer(info, fromFile);
                conts.Add(cont.AssetId, cont);
            }

            for (int i = 0; i < fromFile.dependencies.Count; i++)
            {
                AssetsFileInstance dep = fromFile.GetDependency(am, i);
                if (dep == null)
                    continue;
                string depPath = dep.path.ToLower();
                if (!fileNames.Contains(depPath))
                {
                    RecurseGetAllAssets(dep, conts, files, fileNames);
                }
                else
                {
                    continue;
                }
            }
        }

        private async Task<bool> FailIfNothingSelected()
        {
            if (dataGrid.SelectedItem == null)
            {
                await MessageBoxUtil.ShowDialog(this, "Note", "No item selected.");
                return true;
            }
            return false;
        }

        private AssetInfoDataGridItem GetSelectedGridItem()
        {
            return (AssetInfoDataGridItem)dataGrid.SelectedItem;
        }

        private List<AssetInfoDataGridItem> GetSelectedGridItems()
        {
            return dataGrid.SelectedItems.Cast<AssetInfoDataGridItem>().ToList();
        }

        private List<AssetContainer> GetSelectedAssetsReplaced()
        {
            List<AssetInfoDataGridItem> gridItems = GetSelectedGridItems();
            List<AssetContainer> exts = new List<AssetContainer>();
            foreach (var gridItem in gridItems)
            {
                exts.Add(gridItem.assetContainer);
            }
            return exts;
        }

        private void SetFieldModified(AssetInfoDataGridItem gridItem)
        {
            gridItem.Modified = "*";
            gridItem.Update();
        }

        private void ClearModified()
        {
            foreach (AssetInfoDataGridItem gridItem in dataGrid.Items)
            {
                if (gridItem.Modified != "")
                {
                    gridItem.Modified = "";
                    gridItem.Update();
                }
            }
        }

        private void Workspace_ItemUpdated(AssetsFileInstance file, AssetID assetId)
        {
            int fileId = Workspace.LoadedFiles.IndexOf(file);
            long pathId = assetId.pathID;

            var gridItem = dataGridItems.FirstOrDefault(i => i.FileID == fileId && i.PathID == pathId);

            if (Workspace.LoadedAssets.ContainsKey(assetId))
            {
                //added/modified entry
                if (file != null)
                {
                    AssetContainer? cont = Workspace.GetAssetContainer(file, 0, assetId.pathID);
                    if (cont != null)
                    {
                        if (gridItem != null)
                        {
                            gridItem.assetContainer = cont;
                            SetFieldModified(gridItem);
                        }
                        else
                        {
                            gridItem = AddDataGridItem(cont, true);
                            gridItem.Modified = "*";
                        }
                    }
                }
            }
            else
            {
                //removed entry
                if (gridItem != null)
                {
                    dataGridItems.Remove(gridItem);
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }

    public class AssetInfoDataGridItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Container { get; set; }
        public string Type { get; set; }
        public uint TypeID { get; set; }
        public int FileID { get; set; }
        public long PathID { get; set; }
        public int Size { get; set; }
        public string Modified { get; set; }

        public AssetContainer assetContainer;

        public event PropertyChangedEventHandler? PropertyChanged;

        //ultimate lazy
        public void Update(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
