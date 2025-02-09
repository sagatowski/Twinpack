﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Threading;
using TCatSysManagerLib;

namespace Twinpack.Dialogs
{
    public partial class CatalogWindow : UserControl, INotifyPropertyChanged
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        public event PropertyChangedEventHandler PropertyChanged;

        private PackageContext _context;
        private EnvDTE.Project _plc;

        private Models.ConfigPlcProject _plcConfig;
        private Models.ConfigPlcPackage _packageConfig;
        private IEnumerable<Models.CatalogItem> _catalog = new List<Models.CatalogItem>();
        private List<Models.CatalogItem> _availablePackages = new List<Models.CatalogItem>();
        private List<Models.CatalogItem> _installedPackages = new List<Models.CatalogItem>();
        private SemaphoreSlim _semaphorePackages = new SemaphoreSlim(1, 1);
        private List<Models.PackageVersionGetResponse> _packageVersions;

        private Models.PackageGetResponse _package = new Models.PackageGetResponse();
        private Models.PackageVersionGetResponse _packageVersion = new Twinpack.Models.PackageVersionGetResponse();

        private int _currentCatalogPage = 1;
        private int _currentPackageVersionsPage = 1;
        private int _itemsPerPage = 10;

        private bool _isUpdateAvailable = false;
        private bool _isLoadingPlcConfig = false;
        private bool _isFetchingInstalledPackages = false;
        private bool _isAvailablePackageAvailable = false;
        private bool _isPackageVersionsAvailable = false;        
        private bool _isFetchingAvailablePackages = false;

        private string _searchText = "";
        private TwinpackServer _twinpackServer = new TwinpackServer();
        private Authentication _auth;

        private bool _isBrowsingAvailablePackages;
        private bool _isBrowsingInstalledPackages;
        private bool _isBrowsingUpdatablePackages;

        private bool _isCatalogLoading;
        private bool _isPackageLoading;
        private bool _isPackageVersionLoading;
        private bool _isNewReference;
        private bool _isConfigured;
        private bool _isCreateConfigVisible;
        private bool _isMigrateConfigVisible;
        private bool _isUpdateAllVisible;
        private bool _isRestoreAllVisible;
        private bool _isUpdateAllEnabled;
        private bool _isRestoreAllEnabled;

        private bool _isPackageVersionPanelEnabled;
        private string _installedPackageVersion;

        private bool _forcePackageVersionDownload;
        private bool _forceShowLicense;
        private bool _uninstallDeletes;

        private int _installedPackagesCount;
        private int _updateablePackagesCount;

        public IEnumerable<Models.CatalogItem> Catalog
        {
            get { return _catalog; }
            set
            {
                _catalog = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Catalog)));
            }
        }

        public bool IsFetchingAvailablePackages
        {
            get { return _isFetchingAvailablePackages; }
            set
            {
                _isFetchingAvailablePackages = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFetchingAvailablePackages)));

                if (_isBrowsingAvailablePackages || _isBrowsingUpdatablePackages)
                {
                    if (_isFetchingAvailablePackages)
                        IsCatalogLoading = true;
                    else
                        UpdateCatalog();
                }
            }
        }
        
        public bool IsUpdateAvailable
        {
            get { return _isUpdateAvailable; }
            set
            {
                _isUpdateAvailable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAvailable)));
            }
        }

        public bool IsFetchingInstalledPackages
        {
            get { return _isFetchingInstalledPackages; }
            set
            {
                _isFetchingInstalledPackages = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFetchingInstalledPackages)));

                if (_isBrowsingInstalledPackages)
                {
                    if (_isFetchingInstalledPackages)
                        IsCatalogLoading = true;
                    else
                        UpdateCatalog();
                }
            }
        }

        public bool IsAvailablePackageAvailable
        {
            get { return _isAvailablePackageAvailable; }
            set
            {
                _isAvailablePackageAvailable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAvailablePackageAvailable)));
            }
        }

        public bool IsPackageVersionsAvailable
        {
            get { return _isPackageVersionsAvailable; }
            set
            {
                _isPackageVersionsAvailable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPackageVersionsAvailable)));
            }
        }

        public int InstalledPackagesCount
        {
            get { return _installedPackagesCount; }
            set
            {
                _installedPackagesCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstalledPackagesCount)));
            }
        }

        public int UpdateablePackagesCount
        {
            get { return _updateablePackagesCount; }
            set
            {
                _updateablePackagesCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateablePackagesCount)));
            }
        }

        public Models.PackageGetResponse Package
        {
            get { return _package; }
            set
            {
                _package = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Package)));
            }
        }

        public Models.PackageVersionGetResponse PackageVersion
        {
            get { return _packageVersion; }
            set
            {
                _packageVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageVersion)));
            }
        }

        public List<Models.PackageVersionGetResponse> Versions
        {
            get { return _packageVersions; }
            set
            {
                _packageVersions = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Versions)));
            }
        }

        public bool ForcePackageVersionDownload
        {
            get { return _forcePackageVersionDownload; }
            set
            {
                _forcePackageVersionDownload = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ForcePackageVersionDownload)));
            }
        }

        public bool ForceShowLicense
        {
            get { return _forceShowLicense; }
            set
            {
                _forceShowLicense = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ForceShowLicense)));
            }
        }

        public bool UninstallDeletes
        {
            get { return _uninstallDeletes; }
            set
            {
                _uninstallDeletes = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UninstallDeletes)));
            }
        }

        public bool IsCatalogLoading
        {
            get { return _isCatalogLoading; }
            set
            {
                _isCatalogLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCatalogLoading)));
            }
        }

        public bool IsPackageLoading
        {
            get { return _isPackageLoading; }
            set
            {
                _isPackageLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPackageLoading)));
            }
        }
        public bool IsPackageVersionLoading
        {
            get { return _isPackageVersionLoading; }
            set
            {
                _isPackageVersionLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPackageVersionLoading)));
            }
        }

        public bool IsUpdateAllVisible
        {
            get { return _isUpdateAllVisible; }
            set
            {
                _isUpdateAllVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAllVisible)));
            }
        }

        public bool IsRestoreAllVisible
        {
            get { return _isRestoreAllVisible; }
            set
            {
                _isRestoreAllVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRestoreAllVisible)));
            }
        }

        public bool IsUpdateAllEnabled
        {
            get { return _isUpdateAllEnabled; }
            set
            {
                _isUpdateAllEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAllEnabled)));
            }
        }

        public bool IsRestoreAllEnabled
        {
            get { return _isRestoreAllEnabled; }
            set
            {
                _isRestoreAllEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRestoreAllEnabled)));
            }
        }

        public bool IsCreateConfigVisible
        {
            get { return _isCreateConfigVisible; }
            set
            {
                _isCreateConfigVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCreateConfigVisible)));
            }
        }

        public bool IsMigrateConfigVisible
        {
            get { return _isMigrateConfigVisible; }
            set
            {
                _isMigrateConfigVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMigrateConfigVisible)));
            }
        }

        public bool IsConfigured
        {
            get { return _isConfigured; }
            set
            {
                _isConfigured = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConfigured)));
            }
        }

        public bool IsNewReference
        {
            get { return _isNewReference; }
            set
            {
                _isNewReference = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNewReference)));
            }
        }

        public bool IsPackageVersionPanelEnabled
        {
            get { return _isPackageVersionPanelEnabled; }
            set
            {
                _isPackageVersionPanelEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPackageVersionPanelEnabled)));
            }
        }

        public string InstalledPackageVersion
        {
            get { return _installedPackageVersion; }
            set
            {
                _installedPackageVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstalledPackageVersion)));
            }
        }

        public CatalogWindow(PackageContext context)
        {
            _auth = new Authentication(_twinpackServer);
            _context = context;

            _isBrowsingAvailablePackages = true;

            IsUpdateAllEnabled = true;
            IsRestoreAllEnabled = true;
            InstalledPackagesCount = 0;
            UpdateablePackagesCount = 0;
            Catalog = new List<Models.CatalogItem>();
            DataContext = this;

            InitializeComponent();

            CatalogView.SelectionChanged += Catalog_SelectionChanged;
            BranchesView.SelectionChanged += PackageFilter_SelectionChanged;
            ConfigurationsView.SelectionChanged += PackageFilter_SelectionChanged;
            TargetsView.SelectionChanged += PackageFilter_SelectionChanged;
            VersionsView.SelectionChanged += PackageVersions_SelectionChanged;

            Loaded += Dialog_Loaded;
        }

        private async void Dialog_Loaded(object sender, RoutedEventArgs e)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IsCatalogLoading = true;

            try
            {
                await _auth.LoginAsync(onlyTry: true);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsUpdateAvailable = _twinpackServer.IsClientUpdateAvailable == true;
                btnLogin.Text = _twinpackServer.LoggedIn ? "Logout" : "Login";
                btnRegister.Visibility = _twinpackServer.LoggedIn ? Visibility.Collapsed : Visibility.Visible;
            }

            try
            {
                _plc = TwinpackUtils.ActivePlc(_context.Dte);

                cmbTwinpackServer.Items.Clear();
                cmbTwinpackServer.Items.Add(_twinpackServer.TwinpackUrlBase);
                cmbTwinpackServer.SelectedIndex = 0;

                _isBrowsingAvailablePackages = true;

                await LoadPlcConfigAsync();
                await LoadAvailablePackagesAsync();
                await LoadInstalledPackagesAsync();
                UpdateCatalog();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public async Task LoadPlcConfigAsync()
        {
            if (_plc != null)
            {
                try
                {
                    _isLoadingPlcConfig = true;
                    await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var config = Models.ConfigFactory.Load(Path.GetDirectoryName(_context.Solution.FullName));
                    if (config != null)
                    {
                        _plcConfig = Models.ConfigPlcProjectFactory.MapPlcConfigToPlcProj(config, _plc);
                        IsCreateConfigVisible = false;
                        IsMigrateConfigVisible = false;
                        IsMigrateConfigVisible = _plcConfig?.Packages?.Any() == false && _plcConfig.Frameworks?.Zeugwerk?.References?.Any() == true;
                    }
                    else
                    {
                        IsCreateConfigVisible = true;
                        IsMigrateConfigVisible = false;
                        _plcConfig = await Models.ConfigPlcProjectFactory.CreateAsync(_context.Solution, _plc, _twinpackServer);
                    }

                    IsConfigured = _plcConfig != null;
                }
                catch (Exception ex)
                {
                    IsCreateConfigVisible = true;
                    IsMigrateConfigVisible = false;
                    IsConfigured = false;
                    _plcConfig = null;
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
                }
                finally
                {
                    _isLoadingPlcConfig = false;
                }
            }
            else
            {
                _isLoadingPlcConfig = false;
                IsCreateConfigVisible = false;
                IsMigrateConfigVisible = false;
                IsConfigured = false;
            }
        }
        public async void EditPackageButton_Click(object sender, RoutedEventArgs e)
        {
            var packageId = (CatalogView.SelectedItem as Models.CatalogItemGetResponse)?.PackageId;
            if (packageId == null)
                return;

            var packageVersionId = (VersionsView.SelectedItem as Models.PackageVersionGetResponse)?.PackageVersionId;
            var packagePublish = new PackageVersionWindow(false, _context, _plc, packageId, packageVersionId);
            packagePublish.ShowDialog();

            _twinpackServer.InvalidateCache();

            await LoadInstalledPackagesAsync();
            await LoadAvailablePackagesAsync(SearchTextBox.Text);
            UpdateCatalog();
        }

        public async void UninstallPackageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsPackageVersionPanelEnabled = false;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync(clear: true);
                _logger.Info("Uninstalling package");

                _context.Dte.ExecuteCommand("File.SaveAll");

                await UninstallPackageAsync();

                IsNewReference = true;
                InstalledPackageVersion = null;

                // update config
                _context.Dte.ExecuteCommand("File.SaveAll");
                var config = await WritePlcConfigToConfigAsync(_plcConfig);
                await LoadInstalledPackagesAsync();
                await LoadAvailablePackagesAsync(SearchTextBox.Text);
                UpdateCatalog();

                _logger.Info($"Successfully removed {PackageVersion.Name} from {_plc.Name} references");
                _logger.Info("Finished\n");

            }
            catch (Exception ex)
            {
                _logger.Info("Failed\n");

                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
            }
        }

        public async void AddPackageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsPackageVersionPanelEnabled = false;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync(clear: true);
                _logger.Info("Adding package");

                _context.Dte.ExecuteCommand("File.SaveAll");
                await AddOrUpdatePackageAsync(PackageVersion);
                Package = PackageVersion;
                PackageVersion = PackageVersion;
                InstalledPackageVersion = PackageVersion.Version;

                _context.Dte.ExecuteCommand("File.SaveAll");
                var config = await WritePlcConfigToConfigAsync(_plcConfig);
                await LoadInstalledPackagesAsync();
                await LoadAvailablePackagesAsync(SearchTextBox.Text);
                UpdateCatalog();

                _logger.Info($"Successfully added {PackageVersion.Name} to {_plc.Name} references");
                _logger.Info("Finished\n");

            }
            catch (Exception ex)
            {
                _logger.Info("Failed\n");

                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
            }
        }

        public void UpdatePackageButton_Click(object sender, RoutedEventArgs e)
        {
            AddPackageButton_Click(sender, e);
        }

        public async void RestoreAllPackageButton_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<Models.CatalogItem> items = new List<Models.CatalogItem>();
            try
            {
                IsRestoreAllEnabled = false;
                IsPackageVersionPanelEnabled = false;

                await _semaphorePackages.WaitAsync();
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync(clear: true);
                _logger.Info("Restoring all packages");

                _context.Dte.ExecuteCommand("File.SaveAll");
                items = _installedPackages;
                foreach (var item in items)
                {
                    await AddOrUpdatePackageAsync(item.Installed, showLicenseDialog: false);
                    if (item.PackageId == PackageVersion.PackageId)
                    {
                        Package = item.Installed;
                        PackageVersion = item.Installed;
                        InstalledPackageVersion = PackageVersion.Version;
                    }
                }

                _context.Dte.ExecuteCommand("File.SaveAll");
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _logger.Info("Failed\n");
            }
            finally
            {
                _semaphorePackages.Release();
            }

            try
            {
                var config = await WritePlcConfigToConfigAsync(_plcConfig);
                await LoadInstalledPackagesAsync();
                await LoadAvailablePackagesAsync(SearchTextBox.Text);
                SelectPackageVersionFilter(PackageVersion);
                UpdateCatalog();

                _logger.Info($"Successfully restored {items?.Count()} references");
                _logger.Info("Finished\n");
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _logger.Info("Failed\n");
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
                IsRestoreAllEnabled = true;
            }
        }

        public async void UpdateAllPackageButton_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<Models.CatalogItem> items = new List<Models.CatalogItem>();
            try
            {
                IsPackageVersionPanelEnabled = false;
                IsUpdateAllEnabled = false;

                await _semaphorePackages.WaitAsync();
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync(clear: true);
                _logger.Info("Updating all packages");

                _context.Dte.ExecuteCommand("File.SaveAll");
                items = _installedPackages.Where(x => x.IsUpdateable);
                foreach (var item in items)
                {
                    await AddOrUpdatePackageAsync(item.Update, showLicenseDialog: false);
                    if (item.PackageId == PackageVersion.PackageId)
                    {
                        Package = item.Update;
                        PackageVersion = item.Update;
                        InstalledPackageVersion = PackageVersion.Version;
                    }
                }

                _context.Dte.ExecuteCommand("File.SaveAll");
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _logger.Info("Failed\n");
            }
            finally
            {
                _semaphorePackages.Release();
            }

            try
            { 
                var config = await WritePlcConfigToConfigAsync(_plcConfig);
                await LoadInstalledPackagesAsync();
                await LoadAvailablePackagesAsync(SearchTextBox.Text);
                UpdateCatalog();

                _logger.Info($"Successfully updated {items?.Count()} references to their latest version");
                _logger.Info("Finished\n");
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _logger.Info("Failed\n");
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
                IsUpdateAllEnabled = true;
            }
        }

        public void ShowUpdateablePackages_Click(object sender, RoutedEventArgs e)
        {
            _isBrowsingAvailablePackages = false;
            _isBrowsingUpdatablePackages = true;
            _isBrowsingInstalledPackages = false;
            UpdateCatalog();
        }

        public void ShowInstalledPackages_Click(object sender, RoutedEventArgs e)
        {
            _isBrowsingAvailablePackages = false;
            _isBrowsingUpdatablePackages = false;
            _isBrowsingInstalledPackages = true;
            UpdateCatalog();
        }

        public async void ShowCatalog_Click(object sender, RoutedEventArgs e)
        {
            _isBrowsingAvailablePackages = true;
            _isBrowsingUpdatablePackages = false;
            _isBrowsingInstalledPackages = false;

            try
            {
                if (_searchText != SearchTextBox.Text)
                {
                    await LoadAvailablePackagesAsync(SearchTextBox.Text);
                    _searchText = SearchTextBox.Text;
                }
                UpdateCatalog();
            }
            catch(Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public void UpdateCatalog()
        {
            var text = SearchTextBox.Text;
            if (_isBrowsingAvailablePackages)
            {
                Catalog = _availablePackages.Where(x =>
                     x.DisplayName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.DistributorName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.Name.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
                IsCatalogLoading = IsFetchingAvailablePackages || _isLoadingPlcConfig;
            }
            else if (_isBrowsingInstalledPackages)
            {
                Catalog = _installedPackages.Where(x =>
                     x.DisplayName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.DistributorName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.Name.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
                IsCatalogLoading = IsFetchingInstalledPackages || _isLoadingPlcConfig;
            }
            else if (_isBrowsingUpdatablePackages)
            {
                Catalog = _installedPackages.Where(x => x.IsUpdateable &&
                    (x.DisplayName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.DistributorName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.Name.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0)).ToList();
                IsCatalogLoading = IsFetchingInstalledPackages || _isLoadingPlcConfig;
            }

            IsUpdateAllVisible = _isBrowsingUpdatablePackages && Catalog.Any();
            IsRestoreAllVisible = _isBrowsingInstalledPackages && Catalog.Any();
        }

        public async Task UninstallPackageAsync()
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IsPackageVersionPanelEnabled = false;

            if (PackageVersion.PackageVersionId == null)
                throw new Exception("No packages is selected that could be uninstalled!");

            var plc = _plc.Object as dynamic;
            var sysManager = plc.SystemManager as ITcSysManager2;
            var libManager = sysManager.LookupTreeItem(plc.PathName + "^References") as ITcPlcLibraryManager;

            _context.Dte.ExecuteCommand("File.SaveAll");
            TwinpackUtils.RemoveReference(libManager, Package.Name, Package.Name, PackageVersion.Version, _package.DistributorName);
            _context.Dte.ExecuteCommand("File.SaveAll");

            if (UninstallDeletes)
            {
                _logger.Info($"Uninstalling package {PackageVersion.Name} from system ...");
                TwinpackUtils.UninstallReferenceAsync(libManager, _packageVersion);
            }

            // update config
            _plcConfig.Packages = _plcConfig.Packages.Where(x => x.Name != PackageVersion.Name).ToList();
        }

        public bool IsLicenseDialogRequired(ITcPlcLibraryManager libManager, Models.PackageVersionGetResponse packageVersion, bool showLicenseDialogHint, HashSet<string> shownLicenses)
        {
            var licenseId = TwinpackUtils.ParseLicenseId(packageVersion.LicenseTmcText);
            return (ForceShowLicense || (showLicenseDialogHint && !TwinpackUtils.IsPackageInstalled(libManager, packageVersion))) &&
                   (!string.IsNullOrEmpty(packageVersion.LicenseBinary) || (!string.IsNullOrEmpty(packageVersion.LicenseTmcBinary) && (ForceShowLicense || !shownLicenses.Contains(licenseId))));
        }

        public async Task AddOrUpdatePackageAsync(Models.PackageVersionGetResponse packageVersion, bool showLicenseDialog = true)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (packageVersion.PackageVersionId == null)
                throw new Exception("No packages is selected that could be installed or updated!");

            var cachePath = $@"{Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries";
            var plc = _plc.Object as dynamic;
            var sysManager = plc.SystemManager as ITcSysManager2;
            var libManager = sysManager.LookupTreeItem(plc.PathName + "^References") as ITcPlcLibraryManager;
            var knownLicenseIds = TwinpackUtils.KnownLicenseIds();
            var shownLicenseIds = new HashSet<string>(knownLicenseIds);

            if(IsLicenseDialogRequired(libManager, packageVersion, showLicenseDialog, shownLicenseIds))
            {
                var licenseDialog = new LicenseWindow(libManager, packageVersion);
                if (licenseDialog.ShowLicense() == false)
                {
                    _logger.Warn($"License for {packageVersion.Name} was declined");
                    return;
                }

                shownLicenseIds.Add(TwinpackUtils.ParseLicenseId(packageVersion.LicenseTmcText));
            }

            foreach (var dependency in packageVersion.Dependencies)
            {
                if (IsLicenseDialogRequired(libManager, dependency, showLicenseDialog, shownLicenseIds))
                {
                    var licenseWindow = new LicenseWindow(libManager, dependency);
                    if (licenseWindow.ShowLicense() == false)
                    {
                        _logger.Warn($"License for {packageVersion.Name} was declined");
                        return;
                    }

                    shownLicenseIds.Add(TwinpackUtils.ParseLicenseId(dependency.LicenseTmcText));
                }
            }

            if(packageVersion.LicenseTmcBinary != null)
            {
                _logger.Info($"Copying license description file to TwinCAT for {packageVersion.Name} ...");
                TwinpackUtils.CopyLicenseTmcIfNeeded(packageVersion, knownLicenseIds);
            }

            foreach (var dependency in packageVersion.Dependencies)
            {
                if (dependency.LicenseTmcBinary != null)
                {
                    _logger.Info($"Copying license description file to TwinCAT for {dependency.Name} ...");
                    TwinpackUtils.CopyLicenseTmcIfNeeded(dependency, knownLicenseIds);
                }
            }

            _logger.Info($"Installing package {packageVersion.Name} ...");

            var downloadPackageVersion = await TwinpackUtils.DownloadPackageVersionAndDependenciesAsync(libManager, packageVersion, _twinpackServer, forceDownload: ForcePackageVersionDownload, cachePath: cachePath);
            await TwinpackUtils.InstallPackageVersionsAsync(libManager, downloadPackageVersion, cachePath: cachePath);
            await TwinpackUtils.AddReferenceAsync(libManager, packageVersion.Name, packageVersion.Name, packageVersion.Version, packageVersion.DistributorName);
            IsNewReference = false;

            // update config
            _plcConfig.Packages = _plcConfig.Packages.Where(x => x.Name != packageVersion.Name)
                                                        .Append(new Models.ConfigPlcPackage
                                                        {
                                                            Name = packageVersion.Name,
                                                            Repository = packageVersion.Repository,
                                                            Branch = packageVersion.Branch,
                                                            Configuration = packageVersion.Configuration,
                                                            Target = packageVersion.Target,
                                                            Version = packageVersion.Version,
                                                            DistributorName = packageVersion.DistributorName
                                                        }).ToList();
        }

        public async Task<Models.Config> WritePlcConfigToConfigAsync(Models.ConfigPlcProject plcConfig)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var config = Models.ConfigFactory.Load(Path.GetDirectoryName(_context.Solution.FullName));

            if(config != null)
            {
                Models.ConfigFactory.UpdatePlcProject(config, plcConfig);
                Models.ConfigFactory.Save(config);
            }
            else
            {
                _logger.Warn($"The solution doesn't have a package configuration");
            }

            return config;
        }

        public void ShowLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var licenseDialog = new LicenseWindow(null, PackageVersion);
                licenseDialog.ShowLicense();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public void ShowLicenseTmcButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var licenseDialog = new LicenseWindow(null, PackageVersion);
                licenseDialog.ShowLicense();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }
        public void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(_twinpackServer.RegisterUrl);
        }

        public async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync(clear: true);

                if (!_twinpackServer.LoggedIn)
                {
                    await _auth.LoginAsync();
                }
                else
                {
                    _auth.Logout();
                }
            }
            catch (Exceptions.LoginException ex)
            {
                MessageBox.Show(ex.Message, "Login failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsUpdateAvailable = _twinpackServer.IsClientUpdateAvailable;
                btnLogin.Text = _twinpackServer.LoggedIn ? "Logout" : "Login";
                btnRegister.Visibility = _twinpackServer.LoggedIn ? Visibility.Collapsed : Visibility.Visible;
            }

            try
            {
                _twinpackServer.InvalidateCache();
                await LoadAvailablePackagesAsync();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        private async Task LoadFirstPackageVersionsPageAsync(int? packageId, string branch, string configuration, string target)
        {
            if (packageId == null)
                return;

            await LoadNextPackageVersionsPageAsync((int)packageId, branch, configuration, target, true);
        }

        private async Task LoadNextPackageVersionsPageAsync(int packageId, string branch, string configuration, string target, bool reset = false)
        {
            try
            {
                if (reset)
                    _currentPackageVersionsPage = 1;

                var results = await _twinpackServer.GetPackageVersionsAsync(packageId,
                    branch,
                    configuration,
                    target,
                    _currentPackageVersionsPage,
                    _itemsPerPage);

                IsPackageVersionsAvailable = results.Item2;

                if (reset)
                    Versions = new List<Models.PackageVersionGetResponse>();
            
                Versions = Versions.Concat(results.Item1).ToList();
                _currentPackageVersionsPage++;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
            }
        }

        private async Task LoadAvailablePackagesAsync(string text = "")
        {
            await LoadNextCatalogPageAsync(text, true);
        }

        private async Task LoadNextCatalogPageAsync(string text = "", bool reset = false)
        {
            try
            {
                await _semaphorePackages.WaitAsync();
                IsFetchingAvailablePackages = true;

                if (reset)
                    _currentCatalogPage = 1;

                var results = await _twinpackServer.GetCatalogAsync(text, _currentCatalogPage, _itemsPerPage);
                IsAvailablePackageAvailable = results.Item2;

                if (reset)
                {
                    _availablePackages.Clear();
                }
                foreach (var item in results.Item1)
                {

                    var installedPackage = _installedPackages.FirstOrDefault(x => x.PackageId == item.PackageId);
                    var catalogItem = installedPackage == null ? new Models.CatalogItem(item) : new Models.CatalogItem(installedPackage.Installed);

                    if (installedPackage != null)
                    {
                        catalogItem.Update = installedPackage.Update;
                        catalogItem.Installed = installedPackage.Installed;

                    }

                    _availablePackages.Add(catalogItem);
                }

                _currentCatalogPage++;

            }
            catch (Exception ex)
            {
                // Handle API request error
                Console.WriteLine($"Error fetching items: {ex.Message}");
            }
            finally
            {
                _semaphorePackages.Release();
                IsFetchingAvailablePackages = false;
            }
        }
        private async Task LoadInstalledPackagesAsync()
        {
            try
            {
                IsFetchingInstalledPackages = true;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _semaphorePackages.WaitAsync();

                if (_plcConfig != null)
                {
                    _installedPackages.Clear();
                    foreach (var item in _plcConfig.Packages)
                    {
                        Models.CatalogItem catalogItem = new Models.CatalogItem(item);
                        var packageVersion = await _twinpackServer.GetPackageVersionAsync(item.DistributorName, item.Name, null, item.Configuration, item.Branch, item.Target);

                        if (packageVersion.PackageVersionId != null)
                        {
                            catalogItem = new Models.CatalogItem(packageVersion);
                            catalogItem.Update = packageVersion;
                            catalogItem.Installed = new Models.PackageVersionGetResponse(packageVersion);
                            catalogItem.Installed.Version = item.Version;
                        }

                        var availablePackage = _availablePackages.FirstOrDefault(x => x.PackageId == packageVersion.PackageId);
                        if (availablePackage != null)
                        {
                            availablePackage.Installed = catalogItem.Installed;
                            availablePackage.Update = packageVersion;
                        }

                        _installedPackages.Add(catalogItem);
                    }
                }

                IsNewReference = PackageVersion.PackageVersionId == null || !_installedPackages.Any(x => x.PackageId == PackageVersion.PackageId);
                IsPackageVersionPanelEnabled = _plcConfig != null;
                InstalledPackagesCount = _installedPackages.Count();
                UpdateablePackagesCount = _installedPackages.Where(x => x.IsUpdateable).Count();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                _semaphorePackages.Release();
                IsFetchingInstalledPackages = false;
            }
        }

        private async void ShowMoreAvailablePackagesButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadNextCatalogPageAsync(SearchTextBox.Text);
        }

        private async void ShowMoreAvailableVersionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (PackageVersion.PackageId == null)
                return;

            var branch = BranchesView.SelectedItem as string;
            var configuration = ConfigurationsView.SelectedItem as string;
            var target = TargetsView.SelectedItem as string;
            await LoadNextPackageVersionsPageAsync((int)PackageVersion.PackageId, branch, configuration, target);
        }

        private void SelectPackageVersionFilter(Models.PackageVersionGetResponse installed)
        {
            ConfigurationsView.SelectedIndex = -1;
            BranchesView.SelectedIndex = string.IsNullOrEmpty(installed?.Branch) ? 0 : Package.Branches?.FindIndex(x => x == installed.Branch) ?? -1;
            TargetsView.SelectedIndex = string.IsNullOrEmpty(installed?.Target) ? 0 : Package.Targets?.FindIndex(x => x == installed.Target) ?? -1;
            ConfigurationsView.SelectedIndex = string.IsNullOrEmpty(installed?.Configuration) ? 0 : Package.Configurations?.FindIndex(x => x == installed.Configuration) ?? -1;
        }

        private async void Catalog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = (sender as ListView).SelectedItem as Models.CatalogItem;
            if (item == null)
                return;

            try
            {
                await _semaphorePackages.WaitAsync();

                IsPackageLoading = true;
                IsPackageVersionLoading = true;

                // check if the plc already contains the selected package
                _packageConfig = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == item.Name);

                if (_packageConfig != null)
                    Package = await _twinpackServer.GetPackageAsync(_packageConfig.DistributorName, item.Name);
                else
                    Package = await _twinpackServer.GetPackageAsync(item.DistributorName, item.Name);

                var index = 0;
                if (PackageVersion.PackageVersionId != null && PackageVersion.PackageId == Package.PackageId)
                {
                    InstalledPackageVersion = PackageVersion.Version ?? "";
                    index = _packageVersions.IndexOf(_packageVersions.FirstOrDefault(x => x.PackageVersionId == PackageVersion.PackageVersionId));
                }
                else if (_packageConfig != null)
                {
                    InstalledPackageVersion = _packageConfig.Version ?? "";
                }
                else
                {
                    InstalledPackageVersion = "";
                }

                BranchesView.Visibility = Package?.Branches.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;
                TargetsView.Visibility = Package?.Targets.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;
                ConfigurationsView.Visibility = Package?.Configurations.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;

                SelectPackageVersionFilter(item?.Installed);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                _semaphorePackages.Release();
                IsPackageLoading = false;
            }
        }

        private async void PackageFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (Package.PackageId == null || 
                    BranchesView.SelectedIndex < 0 || TargetsView.SelectedIndex < 0 || ConfigurationsView.SelectedIndex < 0)
                    return;

                IsPackageLoading = Package.PackageId != PackageVersion.PackageId;
                IsPackageVersionLoading = IsPackageLoading;

                var branch = BranchesView.SelectedItem as string;
                var configuration = ConfigurationsView.SelectedItem as string;
                var target = TargetsView.SelectedItem as string;

                await LoadFirstPackageVersionsPageAsync((int)Package.PackageId, branch, configuration, target);

                var item = CatalogView.SelectedItem as Models.CatalogItem;
                var index = Versions?.FindIndex(x => x.Version == item?.Installed?.Version) ?? -1;
                if (index < 0 && item?.Installed != null)
                {
                    index = 0;
                    //Versions = Versions.Concat(new List<Models.PackageVersionGetResponse> { item.Installed }).ToList();
                    //index = Versions.Count() - 1;
                }
                VersionsView.SelectedIndex = string.IsNullOrEmpty(item?.Installed?.Version) ? 0 : index;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageLoading = false;
            }
        }

        private async void PackageVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                IsPackageLoading = Package.PackageId != PackageVersion.PackageId;
                var item = (sender as ComboBox).SelectedItem as Models.PackageVersionGetResponse;

                IsPackageVersionLoading = item?.PackageVersionId != PackageVersion.PackageVersionId;

                if (item != null)
                {
                    PackageVersion = await _twinpackServer.GetPackageVersionAsync(item.DistributorName,
                        item.Name, item.Version, item.Configuration, item.Branch, item.Target,
                        includeBinary: false, cachePath: null);
                }

                IsNewReference = PackageVersion.PackageVersionId == null || !_installedPackages.Any(x => x.PackageId == Package.PackageId);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageLoading = false;
                IsPackageVersionLoading = false;
            }
        }

        public async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync(clear: true);
                _logger.Info("Reloading catalog");

                IsPackageLoading = false;
                Package = new Models.PackageGetResponse();
                PackageVersion = new Models.PackageVersionGetResponse();

                _twinpackServer.InvalidateCache();
                _context.Dte.ExecuteCommand("File.SaveAll");

                await LoadInstalledPackagesAsync();
                await LoadAvailablePackagesAsync(SearchTextBox.Text);
                UpdateCatalog();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public async void CreateConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync(clear: true);
                _logger.Info($"Creating package configuration");

                var config = await Models.ConfigFactory.CreateFromSolutionAsync(_context.Solution, _twinpackServer);

                if (config == null)
                {
                    throw new Exception("Generating the configuration file failed, please create the configuration file manually!");
                }
                else
                {
                    _logger.Info($"Detected {config?.Projects?.SelectMany(x => x.Plcs)?.SelectMany(x => x.Packages)?.Count()} Twinpack packages and {config?.Projects?.SelectMany(x => x.Plcs)?.SelectMany(x => x.References)?.Count()} other references");

                    IsCreateConfigVisible = false;
                    var path = Models.ConfigFactory.Save(config);

                    if (MessageBoxResult.Yes == MessageBox.Show($"The configuration file was successfully created " +
                        $"in {config.FilePath} for your TwinCAT solution, do you want to " +
                        $"review and/or edit it?", "Configuration", MessageBoxButton.YesNo))
                    {
                        Process process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "notepad.exe",
                                Arguments = path,
                                UseShellExecute = true
                            }
                        };

                        process.Start();
                        process.WaitForExit();
                    }

                    await LoadPlcConfigAsync();
                    await LoadInstalledPackagesAsync();
                }

            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public void ShowProjectUrl_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(PackageVersion.ProjectUrl);
        }

        public void UpdateAvailableButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(_twinpackServer?.UserInfo?.UpdateUrl);
        }

        public async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = ((TextBox)sender).Text;

            if (_isBrowsingAvailablePackages)
            {
                _searchText = text;
                await Task.Delay(250);

                if(_searchText == text)
                    await LoadAvailablePackagesAsync(text);
            }
            else
            {
                UpdateCatalog();
            }
        }
    }
}
