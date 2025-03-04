using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MessageBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public class MainWindow : Window
    {
        //controls
        private MenuItem menuOpen;
        private MenuItem menuLoadPackageFile;
        private MenuItem menuClose;
        private MenuItem menuSave;
        private MenuItem menuCompress;
        private MenuItem menuCreateStandaloneInstaller;
        private MenuItem menuCreatePackageFile;
        private MenuItem menuExit;
        private MenuItem menuEditTypeDatabase;
        private MenuItem menuEditTypePackage;
        private MenuItem menuAbout;
        private TextBlock lblFileName;
        private ComboBox comboBox;
        private Button btnExport;
        private Button btnImport;
        private Button btnInfo;

        private AssetsManager am;
        private BundleFileInstance bundleInst;

        private Dictionary<string, BundleReplacer> newFiles;
        private bool changesUnsaved; //sets false after saving
        private bool changesMade; //stays true even after saving
        private bool ignoreCloseEvent;

        public ObservableCollection<ComboBoxItem> comboItems;

        public MainWindow()
        {
            //has to happen BEFORE initcomponent
            Initialized += MainWindow_Initialized;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated items
            menuOpen = this.FindControl<MenuItem>("menuOpen");
            menuLoadPackageFile = this.FindControl<MenuItem>("menuLoadPackageFile");
            menuClose = this.FindControl<MenuItem>("menuClose");
            menuSave = this.FindControl<MenuItem>("menuSave");
            menuCompress = this.FindControl<MenuItem>("menuCompress");
            menuCreateStandaloneInstaller = this.FindControl<MenuItem>("menuCreateStandaloneInstaller");
            menuCreatePackageFile = this.FindControl<MenuItem>("menuCreatePackageFile");
            menuExit = this.FindControl<MenuItem>("menuExit");
            menuEditTypeDatabase = this.FindControl<MenuItem>("menuEditTypeDatabase");
            menuEditTypePackage = this.FindControl<MenuItem>("menuEditTypePackage");
            menuAbout = this.FindControl<MenuItem>("menuAbout");
            lblFileName = this.FindControl<TextBlock>("lblFileName");
            comboBox = this.FindControl<ComboBox>("comboBox");
            btnExport = this.FindControl<Button>("btnExport");
            btnImport = this.FindControl<Button>("btnImport");
            btnInfo = this.FindControl<Button>("btnInfo");
            //generated events
            menuOpen.Click += MenuOpen_Click;
            menuLoadPackageFile.Click += MenuLoadPackageFile_Click;
            menuClose.Click += MenuClose_Click;
            menuSave.Click += MenuSave_Click;
            menuCompress.Click += MenuCompress_Click;
            menuCreatePackageFile.Click += MenuCreatePackageFile_Click;
            menuAbout.Click += MenuAbout_Click;
            btnExport.Click += BtnExport_Click;
            btnImport.Click += BtnImport_Click;
            btnInfo.Click += BtnInfo_Click;
            Closing += MainWindow_Closing;

            newFiles = new Dictionary<string, BundleReplacer>();
            changesUnsaved = false;
            changesMade = false;
            ignoreCloseEvent = false;
        }

        private async void MainWindow_Initialized(object? sender, EventArgs e)
        {
            am = new AssetsManager();
            if (File.Exists("classdata.tpk"))
            {
                am.LoadClassPackage("classdata.tpk");
            }
            else
            {
                await MessageBoxUtil.ShowDialog(this, "Error", "Missing classdata.tpk by exe.\nPlease make sure it exists.");
                Close();
                Environment.Exit(1);
            }
        }

        private async void MenuOpen_Click(object? sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Open assets or bundle file";
            ofd.Filters = new List<FileDialogFilter>() { new FileDialogFilter() { Name = "All files", Extensions = new List<string>() { "*" } } };
            string[] files = await ofd.ShowAsync(this);

            if (files != null && files.Length > 0)
            {
                string selectedFile = files[0];

                DetectedFileType fileType = AssetBundleDetector.DetectFileType(selectedFile);

                CloseAllFiles();

                if (fileType == DetectedFileType.AssetsFile)
                {
                    AssetsFileInstance fileInst = am.LoadAssetsFile(selectedFile, true);

                    if (!await LoadOrAskTypeData(fileInst))
                        return;

                    InfoWindow info = new InfoWindow(am, new List<AssetsFileInstance> { fileInst }, false);
                    info.Show();
                }
                else if (fileType == DetectedFileType.BundleFile)
                {
                    bundleInst = am.LoadBundleFile(selectedFile, false);
                    //don't pester user to decompress if it's only the header that is compressed
                    if (AssetBundleUtil.IsBundleDataCompressed(bundleInst.file))
                    {
                        AskLoadCompressedBundle(bundleInst);
                    }
                    else
                    {
                        if ((bundleInst.file.bundleHeader6.flags & 0x3F) != 0) //header is compressed (most likely)
                            bundleInst.file.UnpackInfoOnly();
                        LoadBundle(bundleInst);
                    }
                }
            }
        }

        private async void MenuLoadPackageFile_Click(object? sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "UABE Mod Installer Package", Extensions = new List<string>() { "emip" } }
            };

            string[] fileList = await ofd.ShowAsync(this);

            if (fileList.Length == 0)
                return;

            string emipPath = fileList[0];

            if (emipPath != null && emipPath != string.Empty)
            {
                AssetsFileReader r = new AssetsFileReader(File.OpenRead(emipPath)); //todo close this
                InstallerPackageFile emip = new InstallerPackageFile();
                emip.Read(r);

                LoadModPackageDialog dialog = new LoadModPackageDialog(emip, am);
                await dialog.ShowDialog(this);
            }
        }

        private void MenuAbout_Click(object? sender, RoutedEventArgs e)
        {
            About about = new About();
            about.ShowDialog(this);
        }

        private async void MenuSave_Click(object? sender, RoutedEventArgs e)
        {
            await AskForLocationAndSave();
        }

        private async void MenuCompress_Click(object? sender, RoutedEventArgs e)
        {
            await AskForLocationAndCompress();
        }

        private async void MenuClose_Click(object? sender, RoutedEventArgs e)
        {
            await AskForSave();
            CloseAllFiles();
        }

        private async void BtnExport_Click(object? sender, RoutedEventArgs e)
        {
            if (bundleInst != null && comboBox.SelectedItem != null)
            {
                int index = (int)((ComboBoxItem)comboBox.SelectedItem).Tag;

                string bunAssetName = bundleInst.file.bundleInf6.dirInf[index].name;
                byte[] assetData = BundleHelper.LoadAssetDataFromBundle(bundleInst.file, index);

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save as...";
                sfd.InitialFileName = bunAssetName;

                string file = await sfd.ShowAsync(this);

                if (file == null)
                    return;

                File.WriteAllBytes(file, assetData);
            }
        }

        private async void BtnImport_Click(object? sender, RoutedEventArgs e)
        {
            if (bundleInst != null)
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "Open";

                string[] files = await ofd.ShowAsync(this);

                if (files.Length == 0)
                    return;

                string file = files[0];

                if (file == null)
                    return;

                byte[] fileBytes = File.ReadAllBytes(file);
                string fileName = Path.GetFileName(file);

                newFiles[fileName] = AssetImportExport.CreateBundleReplacer(fileName, true, fileBytes);

                //todo handle overwriting
                comboItems.Add(new ComboBoxItem()
                {
                    Content = fileName,
                    Tag = comboItems.Count
                });
                comboBox.SelectedIndex = comboItems.Count - 1;

                changesUnsaved = true;
                changesMade = true;
            }
        }

        private async void BtnInfo_Click(object? sender, RoutedEventArgs e)
        {
            if (bundleInst != null && comboBox.SelectedItem != null)
            {
                int index = (int)((ComboBoxItem)comboBox.SelectedItem).Tag;

                string bunAssetName = bundleInst.file.bundleInf6.dirInf[index].name;

                //when we make a modification to an assets file in the bundle,
                //we replace the assets file in the manager. this way, all we
                //have to do is not reload from the bundle if our assets file
                //has been modified
                MemoryStream assetStream;
                if (!newFiles.ContainsKey(bunAssetName))
                {
                    byte[] assetData = BundleHelper.LoadAssetDataFromBundle(bundleInst.file, index);
                    assetStream = new MemoryStream(assetData);
                }
                else
                {
                    //unused if the file already exists
                    assetStream = null;
                }

                //warning: does not update if you import an assets file onto
                //a file that wasn't originally an assets file
                var fileInf = BundleHelper.GetDirInfo(bundleInst.file, index);
                bool isAssetsFile = bundleInst.file.IsAssetsFile(bundleInst.file.reader, fileInf);

                if (isAssetsFile)
                {
                    string assetMemPath = Path.Combine(bundleInst.path, bunAssetName);
                    AssetsFileInstance fileInst = am.LoadAssetsFile(assetStream, assetMemPath, true);

                    if (!await LoadOrAskTypeData(fileInst))
                        return;

                    if (bundleInst != null && fileInst.parentBundle == null)
                        fileInst.parentBundle = bundleInst;

                    InfoWindow info = new InfoWindow(am, new List<AssetsFileInstance> { fileInst }, true);
                    info.Closing += InfoWindow_Closing;
                    info.Show();
                }
                else
                {
                    await MessageBoxUtil.ShowDialog(this,
                        "Error", "This doesn't seem to be a valid assets file.\n" +
                                 "If you want to export a non-assets file,\n" +
                                 "use Export.");
                }
            }
        }

        private async void MenuCreatePackageFile_Click(object? sender, RoutedEventArgs e)
        {
            await MessageBoxUtil.ShowDialog(this, "Not implemented",
                "Bundle pkgs are not supported at the moment.\n" +
                "Trying to install an emip file? Try running\n" +
                "UABEAvalonia applyemip from the command line.");
        }

        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!changesUnsaved || ignoreCloseEvent)
            {
                e.Cancel = false;
                ignoreCloseEvent = false;
            }
            else
            {
                e.Cancel = true;
                ignoreCloseEvent = true;

                await AskForSave();
                Close(); //calling Close() triggers Closing() again
            }
        }

        private void InfoWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sender == null)
                return;

            InfoWindow window = (InfoWindow)sender;

            if (window.Workspace.fromBundle && window.ChangedAssetsDatas != null)
            {
                List<Tuple<AssetsFileInstance, byte[]>> assetDatas = window.ChangedAssetsDatas;

                //file that user initially selected
                AssetsFileInstance firstFile = window.Workspace.LoadedFiles[0];

                foreach (var tup in assetDatas)
                {
                    AssetsFileInstance fileInstance = tup.Item1;
                    byte[] assetData = tup.Item2;

                    string assetName = Path.GetFileName(fileInstance.path);

                    //only modify assets file we opened for now
                    if (fileInstance != firstFile)
                        continue;

                    BundleReplacer replacer = AssetImportExport.CreateBundleReplacer(assetName, true, assetData);
                    newFiles[assetName] = replacer;

                    //replace existing assets file in the manager
                    AssetsFileInstance? inst = am.files.FirstOrDefault(i => i.name.ToLower() == assetName.ToLower());
                    string assetsManagerName;

                    if (inst != null)
                    {
                        assetsManagerName = inst.name;
                        am.files.Remove(inst);
                    }
                    else //shouldn't happen
                    {
                        //we always load bundles from file, so this
                        //should always be somewhere on the disk
                        assetsManagerName = Path.Combine(bundleInst.path, assetName);
                    }

                    MemoryStream assetsStream = new MemoryStream(assetData);
                    am.LoadAssetsFile(assetsStream, assetsManagerName, true);
                }

                changesUnsaved = true;
                changesMade = true;
            }
        }


        private async Task<bool> LoadOrAskTypeData(AssetsFileInstance fileInst)
        {
            string uVer = fileInst.file.typeTree.unityVersion;
            if (am.LoadClassDatabaseFromPackage(uVer) == null)
            {
                VersionWindow version = new VersionWindow(uVer, am.classPackage);
                var newFile = await version.ShowDialog<ClassDatabaseFile>(this);
                if (newFile == null)
                    return false;

                am.classFile = newFile;
            }
            return true;
        }

        private async Task AskForLocationAndSave()
        {
            if (changesUnsaved && bundleInst != null)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save as...";

                string file = await sfd.ShowAsync(this);

                if (file == null)
                    return;

                SaveBundle(bundleInst, file);
            }
        }

        private async Task AskForSave()
        {
            if (changesUnsaved && bundleInst != null)
            {
                ButtonResult choice = await MessageBoxUtil.ShowDialog(this,
                    "Changes made", "You've modified this file. Would you like to save?",
                    ButtonEnum.YesNo);
                if (choice == ButtonResult.Yes)
                {
                    await AskForLocationAndSave();
                }
            }
        }

        private async Task AskForLocationAndCompress()
        {
            if (bundleInst != null)
            {
                //temporary, maybe I should just write to a memory stream or smth
                //edit: looks like uabe just asks you to open a file instead of
                //using your currently opened one, so that may be the workaround
                if (changesMade)
                {
                    ButtonResult continueWithChanges = await MessageBoxUtil.ShowDialog(
                        this, "Note", "You've modified this file, but only file before changes is loaded. If you want to compress the file with " +
                                      "changes, please close this bundle and open the new file. Click Ok to compress the file without changes.",
                        ButtonEnum.OkCancel);

                    if (continueWithChanges == ButtonResult.Cancel)
                    {
                        return;
                    }
                }

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save as...";

                string file = await sfd.ShowAsync(this);

                if (file == null)
                    return;

                const string lz4Option = "LZ4";
                const string lzmaOption = "LZMA";
                const string cancelOption = "Cancel";
                string result = await MessageBoxUtil.ShowDialogCustom(
                    this, "Note", "What compression method do you want to use?\nLZ4: Faster but larger size\nLZMA: Slower but smaller size",
                    lz4Option, lzmaOption, cancelOption);

                AssetBundleCompressionType compType = result switch
                {
                    lz4Option => AssetBundleCompressionType.LZ4,
                    lzmaOption => AssetBundleCompressionType.LZMA,
                    _ => AssetBundleCompressionType.NONE
                };

                if (compType != AssetBundleCompressionType.NONE)
                {
                    CompressBundle(bundleInst, file, compType);
                }
            }
            else
            {
                await MessageBoxUtil.ShowDialog(this, "Note", "Please open a bundle file before using compress.");
            }
        }

        private async void AskLoadCompressedBundle(BundleFileInstance bundleInst)
        {
            const string fileOption = "File";
            const string memoryOption = "Memory";
            const string cancelOption = "Cancel";
            string result = await MessageBoxUtil.ShowDialogCustom(
                this, "Note", "This bundle is compressed. Decompress to file or memory?",
                fileOption, memoryOption, cancelOption);

            if (result == fileOption)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save as...";
                sfd.Filters = new List<FileDialogFilter>() { new FileDialogFilter() { Name = "All files", Extensions = new List<string>() { "*" } } };
                string savePath = await sfd.ShowAsync(this);

                if (savePath == null)
                    return;

                DecompressToFile(bundleInst, savePath);
            }
            else if (result == memoryOption)
            {
                DecompressToMemory(bundleInst);
            }
            else //if (result == cancelOption || result == closeOption)
            {
                return;
            }

            LoadBundle(bundleInst);
        }

        private void DecompressToFile(BundleFileInstance bundleInst, string savePath)
        {
            AssetBundleFile bundle = bundleInst.file;

            FileStream bundleStream = File.Open(savePath, FileMode.Create);
            bundle.Unpack(bundle.reader, new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream), false);

            bundle.reader.Close();
            bundleInst.file = newBundle;
        }

        private void DecompressToMemory(BundleFileInstance bundleInst)
        {
            AssetBundleFile bundle = bundleInst.file;

            MemoryStream bundleStream = new MemoryStream();
            bundle.Unpack(bundle.reader, new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream), false);

            bundle.reader.Close();
            bundleInst.file = newBundle;
        }

        private void LoadBundle(BundleFileInstance bundleInst)
        {
            var infos = bundleInst.file.bundleInf6.dirInf;
            comboItems = new ObservableCollection<ComboBoxItem>();
            for (int i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                comboItems.Add(new ComboBoxItem()
                {
                    Content = info.name,
                    Tag = i
                });
            }
            comboBox.Items = comboItems;
            comboBox.SelectedIndex = 0;

            lblFileName.Text = bundleInst.name;
        }

        private void SaveBundle(BundleFileInstance bundleInst, string path)
        {
            using (FileStream fs = File.OpenWrite(path))
            using (AssetsFileWriter w = new AssetsFileWriter(fs))
            {
                bundleInst.file.Write(w, newFiles.Values.ToList());
            }
            changesUnsaved = false;
        }

        private void CompressBundle(BundleFileInstance bundleInst, string path, AssetBundleCompressionType compType)
        {
            using (FileStream fs = File.OpenWrite(path))
            using (AssetsFileWriter w = new AssetsFileWriter(fs))
            {
                bundleInst.file.Pack(bundleInst.file.reader, w, compType);
            }
        }

        private void CloseAllFiles()
        {
            newFiles.Clear();
            changesUnsaved = false;
            changesMade = false;

            am.UnloadAllAssetsFiles(true);
            am.UnloadAllBundleFiles();

            comboItems = new ObservableCollection<ComboBoxItem>();
            comboBox.Items = comboItems;

            bundleInst = null;

            lblFileName.Text = "No file opened.";
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
