using Microsoft.Win32;
using Newtonsoft.Json;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Render360Video
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        string? filePath, blenderPath;
        Process? process;
        public MainWindow()
        {
            //SfSkinManager.SetTheme(this, new Theme() { ThemeName = "Windows11Dark" });
            InitializeComponent();
            LoadBlenderPath();
            UpdateBlenderVersionUI();


        }
        private void FileSelectorButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "3D Model Files (*.glb;*.blend;*.fbx;*.obj;*.usdz)|*.glb;*.blend;*.fbx;*.obj;*.usdz|All files (*.*)|*.*";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (openFileDialog.ShowDialog() == true)
            {
                // Handle file path
                // You could display the selected file name on the UI or store the file path for processing
                string selectedFilePath = openFileDialog.FileName;
                // Example: Update the button content or another UI element to show the selected file name
                fileSelectorButton.Content = System.IO.Path.GetFileName(selectedFilePath);
                filePath = selectedFilePath;
                Dictionary<string, object> args = new Dictionary<string, object>();

                //get the selected file path
                args.Add("fileName", filePath);

                //convert  json  to string
                string jsonArgs = JsonConvert.SerializeObject(args);



                //call blender with the arguments
                IEnumerable<string> arguments = new List<string> { "-b","--factory-startup",
                                                "Generator/PreviewModel.blend", "-P","Generator/PreviewModel.py",
                                                  "--", jsonArgs };

                // Setup the process start info
                ProcessStartInfo startInfo = new ProcessStartInfo(blenderPath, arguments)
                {
                    CreateNoWindow = true, // Hides the window
                    UseShellExecute = false, // Required to redirect
                    RedirectStandardOutput = true, // Redirects output so it can be read
                    RedirectStandardError = true // Redirects error output
                };

                //destroy image source because of permission errors
                MediaElement.Source = null;

                // Start the process
                using (process = Process.Start(startInfo))
                {
                    // Read the output to the console.
                    Trace.WriteLine(process.StandardOutput.ReadToEnd());
                    Trace.WriteLine(process.StandardError.ReadToEnd());
                    //wait for proccess to finish
                    process.WaitForExit();

                }
                // Load the image from the specified file path
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "PreviewBuffer.png");
                BitmapImage image = new BitmapImage();
                image.ClearValue(BitmapImage.UriSourceProperty);

                image.BeginInit();

                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                image.UriSource = new Uri(path, UriKind.Absolute); // Load the image into memory to avoid file locking

                image.EndInit();

                // Set the image as the source of the MediaElement control
                MediaElement.Source = image.UriSource;
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {


            //renderings here

            Dictionary<string, object> args = new Dictionary<string, object>();

            //get the selected file path
            args.Add("fileName", filePath);
            args.Add("mode", "animation");
            //get selected fps from the combobox
            args.Add("fps", Convert.ToInt32(FpsCombobox.Text));
            //add duration
            args.Add("duration", Convert.ToInt32(DurationText.Text));

            //add quality
            args.Add("quality", QualityCombobx.Text);

            //add width and height from UI
            args.Add("width", Convert.ToInt32(WidthText.Text));
            args.Add("height", Convert.ToInt32(HeightText.Text));

            //add selected lighting type from child of lighting grid ( use name)
            args.Add("lighting", GetSelectedLightingType());

            //add background color
            args.Add("backgroundColor", BackgroundColorCombobox.Text);

            //convert  json  to string
            string jsonArgs = JsonConvert.SerializeObject(args);


            //call blender with the arguments
            IEnumerable<string> arguments = new List<string> {"-b","--factory-startup",
                                            "Generator/PreviewModel.blend", "-P","Generator/PreviewModel.py",
                                              "--", jsonArgs };

            // Setup the process start info
            ProcessStartInfo startInfo = new ProcessStartInfo(blenderPath, arguments)
            {
                CreateNoWindow = true, // Hides the window
                UseShellExecute = false, // Required to redirect
                RedirectStandardOutput = true, // Redirects output so it can be read
                RedirectStandardError = true // Redirects error output

            };

            //empty the media element
            MediaElement.Source = null;


            Trace.WriteLine("Starting Blender Render Process");
            // Start the process
            using (process = Process.Start(startInfo))
            {
                //Read the output to the console.
                Trace.WriteLine(process.StandardOutput.ReadToEnd());
                string error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(error))
                {
                    Trace.WriteLine(error);
                    MessageBox.Show(error+"\nCheck the console for more info...", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                //wait for proccess to finish
                process.WaitForExit();

            }

            Trace.WriteLine("Blender Render Process Started");


            // Load the video from the specified file path
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "VideoBuffer.mp4");
            //print the path
            Trace.WriteLine("Loading video from: " + path);


            MediaElement.Source = new Uri(path, UriKind.Absolute);
            MediaElement.IsEnabled = true;
            MediaElement.LoadedBehavior = MediaState.Manual;
            MediaElement.Play();


            //auto loop, go back to beginning when end (zeg debug)
            MediaElement.MediaEnded += (s, e) =>
            {
                MediaElement.Position = new TimeSpan(0, 0, 1);
                MediaElement.Play();
            };
        }

        private string GetSelectedLightingType()
        {
            // Iterate through each child of the LightingGrid
            foreach (var child in LightingGrid.Children)
            {
                // Check if the child is a RadioButton
                if (child is RadioButton radioButton)
                {
                    // Check if the RadioButton is checked
                    if (radioButton.IsChecked == true)
                    {
                        // Return the Content of the RadioButton as string
                        return radioButton.Content.ToString();
                    }
                }
            }

            // Return null or string.Empty if no RadioButton is selected
            return string.Empty;
        }

        private void UpdateUI(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                action.Invoke();
            }
            else
            {
                Dispatcher.Invoke(action);
            }
        }

        private void OpenBlenderVersionSelector(Object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe",
                Title = "Select Blender Version",
            };

            string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string blenderBasePath = System.IO.Path.Combine(programFilesPath, "Blender Foundation");

            if (Directory.Exists(blenderBasePath))
            {
                var blenderVersionDirectories = Directory.GetDirectories(blenderBasePath, "Blender *");
                var versionedDirectories = blenderVersionDirectories.Select(dir =>
                {
                    var versionMatch = Regex.Match(dir, @"Blender (\d+\.\d+)");
                    if (versionMatch.Success)
                    {
                        Version version;
                        if (Version.TryParse(versionMatch.Groups[1].Value, out version))
                        {
                            return new { DirectoryPath = dir, Version = version };
                        }
                    }
                    return null;
                })
                .Where(x => x != null)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault();

                if (versionedDirectories != null && Directory.Exists(versionedDirectories.DirectoryPath))
                {
                    openFileDialog.InitialDirectory = versionedDirectories.DirectoryPath;
                }
            }

            if (openFileDialog.ShowDialog() == true)
            {
                blenderPath = openFileDialog.FileName;
                UpdateBlenderVersionUI();
                SavePathToAppConfig();
            }
        }

        private void UpdateBlenderVersionUI()
        {
            //save bender path to configuration file

            //update UI
            BlenderVersionText.Text = "Blender Executable Path:" + blenderPath;
        }
        private void SavePathToAppConfig()
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            KeyValueConfigurationCollection settings = config.AppSettings.Settings;

            if (settings["BlenderPath"] == null)
            {
                settings.Add("BlenderPath", blenderPath);
            }
            else
            {
                settings["BlenderPath"].Value = blenderPath;
            }

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);
        }
        private void LoadBlenderPath()
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            KeyValueConfigurationCollection settings = config.AppSettings.Settings;

            if (settings["BlenderPath"] != null)
            {
                blenderPath = settings["BlenderPath"].Value;
                UpdateBlenderVersionUI();
            }
        }


        // You should also define a similar handler for errors if needed.
        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            // Handle error output here if necessary
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //terminate the process
            try
            {
                process?.Kill();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

        }
    

    private void AllowIntegerInput(object sender, TextCompositionEventArgs e)
        {
            // Block input if it's not numeric
            e.Handled = !IsTextAllowed(e.Text);
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!IsTextAllowed(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void SaveOutputButton_Click(object sender, RoutedEventArgs e)
        {
            //open a filepicker to save a mp4 file with the default directory being the documents folder
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "MP4 Files (*.mp4)|*.mp4",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                FileName = "RenderedVideo.mp4"
            };
            //if the user selects a path
            if (saveFileDialog.ShowDialog() == true)
            {
                //copy the video file to the selected path
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "VideoBuffer.mp4");
                File.Copy(path, saveFileDialog.FileName, true);
            }
        }

        private static bool IsTextAllowed(string text)
        {
            // Regular expression to match non-numeric input
            return !Regex.IsMatch(text, "[^0-9]+");
        }

       
    }
}
