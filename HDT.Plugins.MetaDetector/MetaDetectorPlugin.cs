using System;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Plugins;
using HDT.Plugins.MetaDetector.Controls;
using HDT.Plugins.MetaDetector.Logging;
using System.Threading.Tasks;

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
            if (_MainWindow == null)
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
        }

        public void OnUnload()
        {
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
            get { return new Version(0, 0, 7); }
        }

        private async void CheckForUpdate()
        {
            var latest = await GitHub.CheckForUpdate("adnanc", "HDT.Plugins.MetaDetector", Version);
            if (latest != null)
            {
                _MainWindow.newVersionAvailable();
                VersionWindow newVersion = new VersionWindow();
                newVersion.Show();
            }
        }
    }
}
