using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Windows.Storage.Search;
using Windows.Storage.Pickers;
using Windows.Storage.AccessCache;
using System.Threading;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AstoriaPackageInstaller
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        StorageFile packageInContext;
        List<Uri> dependencies = new List<Uri>();
        //ValueSet cannot contain values of the URI class which is why there is another list below.
        //This is required to update the progress in a notification using a background task.
        List<string> dependenciesAsString = new List<string>();

        bool pkgRegistered = false;
        public MainPage()
        {
            this.InitializeComponent();

        }

        /// <summary>
        /// Attempts to get appx/appxbundle from the OnFileActivated event in App.xaml.cs
        ///If the cast fails in the try statement then the catch statement will change
        ///the UI so the user can load the required files themselves.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {

            base.OnNavigatedTo(e);
            try
            {
                StorageFile package = (StorageFile)e.Parameter;
                packageInContext = package;
                updateUIForPackageInstallation();
            }
            catch (Exception x)
            {
                Debug.WriteLine(x.Message);
                permissionTextBlock.Text = "Load an Astoria .appx and/or OBB Folder";
                installProgressBar.Visibility = Visibility.Collapsed;
                installValueTextBlock.Visibility = Visibility.Collapsed;
                installButton.Visibility = Visibility.Collapsed;
                cancelButton.Content = "Exit";
                packageNameTextBlock.Text = "No package Selected";

            }
        }

        private void updateUIForPackageInstallation()
        {
            packageNameTextBlock.Text = packageInContext.DisplayName;
            loadFileButton.Content = "Load a different file";

        }



        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        /// <summary>
        /// <para>
        /// Installs the the package with or without it's dependencies depending on whether the user loads their dependecies or not.
        /// The AddPackageAsync method uses the Uri of the files used to install the packages and dependencies.
        /// </para>
        /// <para>
        /// WARNING: In order to use some PackageManager class' methods, restricted capabilities need to be added to 
        /// the appxmanifest. In this case, the restricted capability that has been added is the "packageManagement".
        /// </para>
        /// If they are not added, to your app and you use certain methods, your app will crash unexpectedly.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void installButton_Click(object sender, RoutedEventArgs e)
        {
            loadFileButton.Visibility = Visibility.Collapsed;
            loadDependenciesButton.Visibility = Visibility.Collapsed;
            installButton.Visibility = Visibility.Collapsed;
            cancelButton.Visibility = Visibility.Collapsed;

            //Modern Test:
            //showProgressInNotification();

            //Legacy Test:
            //showProgressInApp();

            //Normal Code:
            //If the device is on the creators update or later, install progress is shown in the action center and App UI
            //Otherwise, all progress is shown in the App's UI.
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 4))
            {
                showProgressInNotification();
            }
            else
            {
                showProgressInApp();
            }




        }


        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        DeploymentResult resultInstaller;
        string resultText = "Nothing";
        private async void showProgressInApp()
        {
            installProgressBar.Visibility = Visibility.Visible;
            installValueTextBlock.Visibility = Visibility.Visible;
            PackageManager pkgManager = new PackageManager();
            Progress<DeploymentProgress> progressCallback = new Progress<DeploymentProgress>(installProgress);
            notification.showInstallationHasStarted(packageInContext.Name);
            textBlock.Text = string.Empty;
            if (dependencies != null)
                try
                {
                    callDownloadMonitorTimer();
                    resultInstaller = await pkgManager.AddPackageAsync(new Uri(packageInContext.Path), null, DeploymentOptions.None).AsTask(cancellationTokenSource.Token, progressCallback);
                    checkIfPackageRegistered(resultInstaller, resultText);
                }

                catch (Exception e)
                {
                    resultText = e.Message;
                }



            cancelButton.Content = "Exit";
            cancelButton.Visibility = Visibility.Visible;
            if (pkgRegistered == true)
            {
                permissionTextBlock.Text = "Completed";
                notification.ShowInstallationHasCompleted(packageInContext.Name);



            }
            else if (pkgRegistered == false)
            {
                resultTextBlock.Text = packageInContext.Name + " is Installed Successfully, Check the App launcher";
                notification.ShowInstallationHasCompleted(packageInContext.Name);
            }
        }

        private void checkIfPackageRegistered(DeploymentResult result, string resultText)
        {
            if (result.IsRegistered)
            {
                pkgRegistered = true;
            }
            else
            {
                resultText = result.ErrorText;
            }
        }

        public Timer InstallMonitorTimer;
        public void callDownloadMonitorTimer(bool startState = false)
        {
            try
            {
                InstallMonitorTimer?.Dispose();
                if (startState)
                {
                    try
                    {
                        cancellationTokenSource.Cancel();
                    }
                    catch (Exception e)
                    {

                    }
                    try
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }
                    catch (Exception e)
                    {

                    }
                    InstallMonitorTimer = new Timer(async delegate
                    {

                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            try
                            {
                                if (resultInstaller != null)
                                {
                                    checkIfPackageRegistered(resultInstaller, resultText);
                                    if (pkgRegistered)
                                    {
                                        try
                                        {
                                            cancellationTokenSource.Cancel();
                                        }
                                        catch (Exception ec)
                                        {

                                        }
                                        installProgressBar.Value = 100;
                                        string percentageAsString = String.Format($"100%");
                                        installValueTextBlock.Text = percentageAsString;
                                        cancelButton.Content = "Exit";
                                        cancelButton.Visibility = Visibility.Visible;
                                        permissionTextBlock.Text = "Completed";
                                        notification.ShowInstallationHasCompleted(packageInContext.Name);
                                        callDownloadMonitorTimer();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {

                            }
                        });
                    }, null, 0, 1000);
                }
            }
            catch (Exception e)
            {

            }
        }

        /// <summary>
        /// Passes package file path and of file paths dependencies into the backgroundTask
        /// using a ValueSet.
        /// </summary>
        private async void showProgressInNotification()
        {
            permissionTextBlock.Text = "Check Your Notifications/Action Center 😉";
            var thingsToPassOver = new ValueSet();
            thingsToPassOver.Add("packagePath", packageInContext.Path);
            if (dependenciesAsString != null & dependenciesAsString.Count > 0)
            {
                int count = dependenciesAsString.Count();
                for (int i = 0; i < count; i++)
                {
                    thingsToPassOver.Add($"dependencies{i}", dependenciesAsString[i]);
                }
                thingsToPassOver.Add("installType", 1);
            }
            else
            {
                thingsToPassOver.Add("installType", 0);
            }

            PackageManager pkgManager = new PackageManager();
            ApplicationTrigger appTrigger = new ApplicationTrigger();
            var backgroundTask = RegisterBackgroundTask("installTask.install", "installTask", appTrigger);
            //backgroundTask.Completed += new BackgroundTaskCompletedEventHandler(OnCompleted);
            backgroundTask.Progress += new BackgroundTaskProgressEventHandler(OnProgress);
            var result = await appTrigger.RequestAsync(thingsToPassOver);

            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == "installTask")
                {
                    AttachCompletedHandler(task.Value);

                }
            }
            installProgressBar.Visibility = Visibility.Visible;
            installValueTextBlock.Visibility = Visibility.Visible;
        }

        private async void OnProgress(BackgroundTaskRegistration sender, BackgroundTaskProgressEventArgs args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {

                installProgressBar.Value = args.Progress;
                installValueTextBlock.Text = $"{args.Progress}%";
            });
        }

        private void AttachCompletedHandler(IBackgroundTaskRegistration task)
        {
            task.Completed += new BackgroundTaskCompletedEventHandler(OnCompleted);
        }


        private async void OnCompleted(IBackgroundTaskRegistration task, BackgroundTaskCompletedEventArgs args)
        {
            //UpdateUI;
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                cancelButton.Content = "Exit";
                cancelButton.Visibility = Visibility.Visible;
                permissionTextBlock.Text = "Install Task Complete, check notifications for results";


            });
        }



        public static BackgroundTaskRegistration RegisterBackgroundTask(string taskEntryPoint,
                                                                            string taskName,
                                                                            IBackgroundTrigger trigger)
        {
            //
            // Check for existing registrations of this background task.
            //

            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {

                if (cur.Value.Name == taskName)
                {
                    //
                    // The task is already registered.
                    //

                    return (BackgroundTaskRegistration)(cur.Value);
                }
            }

            //
            // Register the background task.
            //

            var builder = new BackgroundTaskBuilder();

            builder.Name = taskName;
            builder.TaskEntryPoint = taskEntryPoint;
            builder.SetTrigger(trigger);

            BackgroundTaskRegistration task = builder.Register();

            return task;
        }



        /// <summary>
        /// Updates the progress bar and status of the installation in the app's UI.
        /// </summary>
        /// <param name="installProgress"></param>
        private void installProgress(DeploymentProgress installProgress)
        {

            double installPercentage = installProgress.percentage;
            permissionTextBlock.Text = "Installing...";
            installProgressBar.Value = installPercentage;
            string percentageAsString = String.Format($"{installPercentage}%");
            installValueTextBlock.Text = percentageAsString;

        }

        /// <summary>
        /// Retreives an appx/appxbundle file using the file picker
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void loadFileButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.FileTypeFilter.Add(".appx");
            picker.FileTypeFilter.Add(".appxbundle");
            picker.FileTypeFilter.Add(".msix");
            picker.FileTypeFilter.Add(".msixbundle");

            Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                //UI changes to allow the user to install the package
                packageInContext = file;
                permissionTextBlock.Text = "Do you want to install this package?";
                installButton.Visibility = Visibility.Visible;
                cancelButton.Content = "Cancel";
                packageNameTextBlock.Text = packageInContext.DisplayName;
                loadFileButton.Content = "Load a different file";
            }
        }

        /// <summary>
        /// Opens the FolderPicker to select app data files, and transfer them to 
        /// "C:\Data\Users\DefApps\AppData\Local\aow\mnt\shell\emulated\0\Android\obb"
        /// The issue is that the user must select the destination folder once for it to be saved.
        /// This is proveded with a Folder Junction made from the desired location above and W10M Public path:
        /// "C:\Data\Users\Public\Android Storage\Android\obb
        /// "
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        ///



        private async void loadDependenciesButton_Click(object sender, RoutedEventArgs e)
        {
            // Choose folder
            dependencies = new List<Uri>();
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.FileTypeFilter.Add("*");

            Windows.Storage.StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder != null)

            {
                // call the output folder picker and copy to there
                var outfolder = await GetAndroidFolder();
                copyFiles(folder, outfolder);
            }
            else
            {
                resultTextBlock.Text = "No Folder Selected";
            }

        }
        // Copy selected folder
        private async void copyFiles(StorageFolder targetFolder, StorageFolder destinationFolder)
        {
            var newFolder = await destinationFolder.CreateFolderAsync(targetFolder.Name, CreationCollisionOption.ReplaceExisting);

            var folders = targetFolder.CreateFileQuery(CommonFileQuery.OrderByName);

            var fileSizeTasks = (await folders.GetFilesAsync()).Select(async file => (await file.CopyAsync(newFolder, file.Name, NameCollisionOption.ReplaceExisting)));

            await Task.WhenAll(fileSizeTasks);
        }
        // Remember the selected folder
        public static async Task<StorageFolder> GetAndroidFolder()
        {
            StorageFolder AndroidFolder = null;

            var fileToken = Plugin.Settings.CrossSettings.Current.GetValueOrDefault("AndroidStorage", "");
            if (fileToken.Length > 0)
            {
                AndroidFolder = await GetFileForToken(fileToken);
            }
            if (AndroidFolder == null)
            {
                var folderPicker = new FolderPicker();
                folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
                folderPicker.FileTypeFilter.Add("*");

                StorageFolder DownloadsFolderTest = await folderPicker.PickSingleFolderAsync();
                if (DownloadsFolderTest != null)
                {
                    AndroidFolder = DownloadsFolderTest;
                    RememberFile(AndroidFolder);
                }
            }

            return AndroidFolder;
        }
        public static string RememberFile(StorageFolder file)
        {
            string token = Guid.NewGuid().ToString();
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, file);
            Plugin.Settings.CrossSettings.Current.AddOrUpdateValue("AndroidStorage", token);
            return token;
        }
        public static async Task<StorageFolder> GetFileForToken(string token)
        {
            if (!StorageApplicationPermissions.FutureAccessList.ContainsItem(token)) return null;
            return await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
        }

        private void textBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }
    }
}

