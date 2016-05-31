using System;
using System.IO;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Plugins;
using HDT.Plugins.MetaDetector.Controls;

namespace HDT.Plugins.MetaDetector
{
    public class MetaDetectorPlugin : IPlugin
    {
        private MenuItem _MetaDetectorMenuItem;
        private OpDeckWindow _MainWindow = null;
        private MetaDetector _MetaDetector;
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
            get { return "Show which deck opponent might be playing"; }
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
            _MainWindow = new OpDeckWindow();

            _MetaDetectorMenuItem = new PluginMenu(_MainWindow);

            _MetaDetector = new MetaDetector(_MainWindow);

            GameEvents.OnTurnStart.Add(_MetaDetector.TurnStart);
            GameEvents.OnOpponentPlay.Add(_MetaDetector.OpponentPlay);
            GameEvents.OnGameStart.Add(_MetaDetector.GameStart);
            GameEvents.OnGameEnd.Add(_MetaDetector.GameEnd);

            //_MainWindow.Show();
        }

        public void OnUnload()
        {
            if (_MetaDetector._statsUpdated)
                _MetaDetector.SendDeckStats();
        }

        public void OnUpdate()
        {

        }

        public Version Version
        {
            get { return new Version(0, 0, 1); }
        }
    }
}
