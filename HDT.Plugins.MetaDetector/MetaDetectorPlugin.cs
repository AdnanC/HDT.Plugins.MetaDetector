using System;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Plugins;
using HDT.Plugins.MetaDetector.Controls;
using HDT.Plugins.MetaDetector.Logging;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using Hearthstone_Deck_Tracker;
using System.ComponentModel;
using System.Linq;
using System.Xml.Linq;

namespace HDT.Plugins.MetaDetector
{
    public class MetaDetectorPlugin : IPlugin
    {
        private MenuItem _MetaDetectorMenuItem;
        private OpDeckWindow _MainWindow = null;
        private MetaDetector _MetaDetector = null;

        public string Author
        {
            get { return "AdnanC"; }
        }

        public string ButtonText
        {
            get { return "Settings"; }
        }

        public string Description
        {
            get { return "Shows which deck opponent might be playing"; }
        }

        public MenuItem MenuItem
        {
            //get { return null; }
            get { return _MetaDetectorMenuItem; }
        }

        public string Name
        {
            get { return "Meta Detector"; }
        }

        public void OnButtonPress()
        {

        }

        public void OnLoad()
        {

            var xml = XDocument.Load(Config.Instance.DataDir + @"\plugins.xml");

            // Query the data and write out a subset of contacts
            /*var query = from c in xml.Root.Descendants("ArrayOfPluginSettings")
                        where c.Element("FileName").Value.ToString() == "Plugins/MetaStats/MetaStats.dll"
                        select c.Element("IsEnabled");*/

            var allPlugins = xml.Root.Descendants("PluginSettings").Where(x => x.Element("IsEnabled").Value == "true");

            //MetaLog.Info("Testing XML: " + allPlugins);

            foreach (var enabledPluging in allPlugins)
            {
                if (enabledPluging.Element("Name").Value.Trim() == "Meta Stats")
                {
                    VersionWindow _ver = new VersionWindow();
                    _ver.Show();
                    //throw new Exception("MetaStats Plugin already Enabled. Please Disable that first.");
                }
            }

            try
            {

                _MainWindow = new OpDeckWindow();

                _MetaDetectorMenuItem = new PluginMenu(_MainWindow);

                _MetaDetector = new MetaDetector(_MainWindow);

                _MainWindow.updateVersion(Version);

                GameEvents.OnGameStart.Add(_MetaDetector.GameStart);
                GameEvents.OnGameEnd.Add(_MetaDetector.GameEnd);

                GameEvents.OnTurnStart.Add(_MetaDetector.TurnStart);

                GameEvents.OnOpponentPlay.Add(_MetaDetector.OpponentPlay);
                GameEvents.OnOpponentDraw.Add(_MetaDetector.OpponentDraw);

                GameEvents.OnOpponentCreateInPlay.Add(_MetaDetector.OpponentCreateInPlay);
                GameEvents.OnOpponentCreateInDeck.Add(_MetaDetector.OpponentCreateInDeck);
                GameEvents.OnOpponentHeroPower.Add(_MetaDetector.OpponentHeroPower);
                GameEvents.OnOpponentSecretTriggered.Add(_MetaDetector.OpponentSecretTriggered);
                GameEvents.OnOpponentPlayToGraveyard.Add(_MetaDetector.OpponentPlayToGraveyard);
                GameEvents.OnOpponentMulligan.Add(_MetaDetector.OpponentMulligan);

                GameEvents.OnPlayerDraw.Add(_MetaDetector.PlayerDraw);
                GameEvents.OnPlayerPlay.Add(_MetaDetector.PlayerPlay);
                GameEvents.OnPlayerCreateInPlay.Add(_MetaDetector.PlayerCreateInPlay);
                GameEvents.OnPlayerCreateInDeck.Add(_MetaDetector.PlayerCreateInDeck);
                GameEvents.OnPlayerHeroPower.Add(_MetaDetector.PlayerHeroPower);
                GameEvents.OnPlayerMulligan.Add(_MetaDetector.PlayerMulligan);

                CheckForUpdate();

                //_MainWindow.Show();
                //_MainWindow.Visibility = System.Windows.Visibility.Hidden;
                MetaLog.Info("Plugin Load Successful");

            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
                MetaLog.Info("Plugin Load Unsuccessful");
            }
        }

        public void OnUnload()
        {
            _MetaDetector.saveConfig();
            _MainWindow.Close();
            _MetaDetector = null;
            _MainWindow = null;
            MetaLog.Info("Plugin Unload Successful");
        }

        public void OnUpdate()
        {

        }

        public Version Version
        {
            get { return new Version(0, 0, 10); }
        }

        private async void CheckForUpdate()
        {
            try
            {
                var latest = await GitHub.CheckForUpdate("adnanc", "HDT.Plugins.MetaDetector", Version);
                if (latest != null)
                {
                    //_MainWindow.newVersionAvailable();
                    //VersionWindow newVersion = new VersionWindow();
                    //newVersion.Show();
                    string pluginDLL = Path.Combine(Config.Instance.DataDir, @"Plugins\MetaDetector\MetaDetector.tmp");

                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadFileCompleted += (wc_DownloadFileCompleted);
                        wc.DownloadFileAsync(new Uri("https://s3.amazonaws.com/metadetector/MetaDetector.dll"), pluginDLL);
                    }
                }
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }

        void wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                string tempFile = Path.Combine(Config.Instance.DataDir, @"Plugins\MetaDetector\MetaDetector.tmp");
                string pluginDLL = Path.Combine(Config.Instance.DataDir, @"Plugins\MetaDetector\MetaDetector.dll");
                if (File.Exists(tempFile))
                {
                    File.Copy(tempFile, pluginDLL, true);
                    File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }
    }
}
