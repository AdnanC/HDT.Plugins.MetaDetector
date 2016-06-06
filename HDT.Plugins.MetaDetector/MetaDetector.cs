using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;
using System.Net;
using System.Xml.Serialization;
//using System.Net.Http;
using System.Threading.Tasks;
//using System.Web.Script.Serialization;
using System.ComponentModel;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using HDT.Plugins.MetaDetector.Controls;
using System.Text;
using System.Collections.Specialized;
using System.Configuration;

namespace HDT.Plugins.MetaDetector
{

    public class MetaDetector
    {
        private OpDeckWindow _mainWindow;
        private Deck _lastGuessDeck;

        private bool _validGameMode = true;

        private List<Deck> _metaDecks;
        private List<Deck> _matchedDecks;
        private List<Card> _opponentCardsPlayed;
        private MyConfig _appConfig;
        private static string _deckDirectory = Path.Combine(Config.AppDataPath, @"MetaDetector");
        private static string _deckFilename = Path.Combine(_deckDirectory, @"metaDecks.xml");
        private int _opponentCardCheck = 4;
        private int _opponentCardCount = 0;
        private int _opponentTurnCount = 0;
        internal bool _statsUpdated = false;

        public MetaDetector(OpDeckWindow mainWindow)
        {
            _mainWindow = new OpDeckWindow();
            _mainWindow = mainWindow;
            _lastGuessDeck = new Deck();

            _metaDecks = new List<Deck>();
            _matchedDecks = new List<Deck>();
            _statsUpdated = false;

            _opponentCardsPlayed = new List<Card>();

            _appConfig = MyConfig.Load();
            _appConfig.Save();

            LoadMetaDecks();
        }

        internal void GameStart()
        {
            if (Core.Game.CurrentGameMode == GameMode.Arena ||
                Core.Game.CurrentGameMode == GameMode.Brawl ||
                Core.Game.CurrentGameMode == GameMode.Spectator)
                _validGameMode = false;
            else
                _validGameMode = true;

            if (_validGameMode)
            {
                _opponentCardCheck = 4;
                _opponentCardCount = 0;
                _opponentTurnCount = 0;

                _mainWindow.Show();
                _mainWindow.updateCardsCount(_opponentCardCount);
                _mainWindow.resetWindow(_metaDecks);
            }
        }

        internal void TurnStart(ActivePlayer activePlayer)
        {
            if (_validGameMode)
            {
                if (ActivePlayer.Player == activePlayer)
                {
                    updateDecks();
                }
                else
                {
                    _opponentTurnCount++;
                }
            }
        }

        internal void OpponentPlay(Card cardPlayed)
        {
            if (_validGameMode)
            {
                _opponentCardsPlayed.Add(cardPlayed);

                if (cardPlayed.Id != "GAME_005") //ignore the coin
                {
                    _opponentCardCount++;
                    updateDecks();
                }
            }
        }

        internal void GameEnd()
        {
            if (_validGameMode)
                try
                {
                    if (_statsUpdated)
                        SaveMetaDeckStats();
                    _matchedDecks = _metaDecks;

                    _mainWindow.updateText("Waiting for new Game...", Brushes.White);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
        }

        private void updateDecks()
        {
            if (_validGameMode)
                try
                {
                    List<Deck> displayDecks = new List<Deck>();

                    if (_opponentCardCount > 0)
                    {
                        displayDecks = matchMetaDeck();

                        if (_matchedDecks.Count == 0 || _matchedDecks == null)
                        {
                            _mainWindow.updateText("No Decks Found.", Brushes.IndianRed);
                            return;
                        }
                        else if (_matchedDecks.Count > 0)
                        {
                            _mainWindow.updateCardsCount(_opponentCardCount);

                            if (_opponentCardCount > _opponentCardCheck)
                            {
                                _opponentCardCheck = _opponentCardCount;

                                if (_opponentCardCount > 5)
                                {
                                    int count = _opponentCardCount;
                                    foreach (Deck d in _matchedDecks)
                                    {
                                        _metaDecks.Find(x => x.DeckId == d.DeckId).Note = (Convert.ToInt32(_metaDecks.Find(x => x.DeckId == d.DeckId).Note) + count).ToString();
                                    }
                                    _statsUpdated = true;
                                }
                            }
                        }

                        _mainWindow.updateDeckList(displayDecks);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
        }

        public bool checkNewVersion()
        {
            string currentVersion = _appConfig.currentVersion;
            DateTime lastCheck = _appConfig.lastCheck;

            if ((DateTime.Now - lastCheck).TotalDays > 3)
            {
                WebClient client = new WebClient();
                String versionNumber = client.DownloadString("http://www.desi-radio.com/metaversion.php");

                if (versionNumber.Trim() != "")
                {
                    if (versionNumber != currentVersion)
                    {
                        DownloadMetaFile();

                        _appConfig.currentVersion = versionNumber;
                        _appConfig.lastCheck = DateTime.Now;
                        _appConfig.Save();
                        return true;
                    }
                }
            }

            return false;
        }

        private void LoadMetaDecks()
        {
            try
            {
                if (checkNewVersion())
                    return;

                if (File.Exists(_deckFilename))
                {
                    _metaDecks = XmlManager<List<Deck>>.Load(_deckFilename);
                    _matchedDecks = _metaDecks;

                    //_mainWindow.updateDeckList(_metaDecks);
                    //Log.Info(code.ToString());
                }
                else
                {
                    if (!Directory.Exists(_deckDirectory))
                        Directory.CreateDirectory(_deckDirectory);

                    DownloadMetaFile();
                    //_metaDecks.Clear();
                    //XmlManager<List<Deck>>.Save(_deckFilename, _metaDecks);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public static void DecompressFile(FileInfo fileToDecompress)
        {
            using (FileStream originalFileStream = fileToDecompress.OpenRead())
            {
                string currentFileName = fileToDecompress.FullName;
                string newFileName = currentFileName.Remove(currentFileName.Length - fileToDecompress.Extension.Length);

                using (FileStream decompressedFileStream = File.Create(newFileName))
                {
                    using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                    {
                        decompressionStream.CopyTo(decompressedFileStream);
                    }
                }
            }
        }

        private void DownloadMetaFile()
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                    wc.DownloadFileCompleted += (wc_DownloadFileCompleted);
                    wc.DownloadFileAsync(new Uri("https://s3.amazonaws.com/metadetector/metaDecks.xml.gz"), _deckFilename + ".gz");
                }
            }
            catch (Exception ex)
            {
                _mainWindow.updateText("Unable to download Meta File.", Brushes.PaleVioletRed);
                Log.Error(ex);
            }
        }


        void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            _mainWindow.updateText("Downloading Meta File: " + e.ProgressPercentage + "%", Brushes.Orange);
        }

        void wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            try
            {

                _mainWindow.updateText("Meta File Downloaded", Brushes.LightGreen);
                FileInfo fi = new FileInfo(_deckFilename + ".gz");
                DecompressFile(fi);

                if (File.Exists(_deckFilename))
                {
                    _metaDecks = XmlManager<List<Deck>>.Load(_deckFilename);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void LoadMetaDeckfromZip()
        {
            try
            {
                using (var file = File.OpenRead(_deckFilename))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        using (var stream = entry.Open())
                        {
                            StreamReader reader = new StreamReader(stream);
                            string text = reader.ReadToEnd();
                            _metaDecks = XmlManager<List<Deck>>.LoadFromString(text);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public List<Deck> matchMetaDeck()
        {
            if (_validGameMode)
            {
                try
                {
                    var validDecks = _metaDecks.Where(x => x.Class == Core.Game.Opponent.Class).ToList();

                    var cardEntites = Core.Game.Opponent.RevealedEntities.Where(x => (x.IsMinion || x.IsSpell || x.IsWeapon) && !x.Info.Created && !x.Info.Stolen).GroupBy(x => x.CardId).ToList();

                    if (validDecks.Count > 1 && cardEntites != null)
                        validDecks = validDecks.Where(x => cardEntites.All(ce => x.GetSelectedDeckVersion().Cards.Any(c => c.Id == ce.Key && c.Count >= ce.Count()))).ToList();


                    if (validDecks.Count == 0)
                    {
                        _mainWindow.updateText("No Match Found. Showing Closest Decks", Brushes.YellowGreen);

                        validDecks = _metaDecks.Where(x => x.Class == Core.Game.Opponent.Class).ToList();
                        validDecks = validDecks.Where(x => x.Cards.Contains((Card)_opponentCardsPlayed.Where(p => p.Count > 0))).ToList();
                        _matchedDecks = validDecks;

                        if (validDecks.Count > 10)
                            validDecks = validDecks.Where(x => x.Cards.Contains((Card)_opponentCardsPlayed.Where(p => p.Count > 0))).Take(10).ToList();

                        return validDecks;
                    }

                    _matchedDecks = validDecks;

                    if (validDecks.Count > 20)
                        validDecks = validDecks.Where(x => cardEntites.Any(ce => x.GetSelectedDeckVersion().Cards.Any(c => c.Id == ce.Key))).Take(20).ToList();

                    _mainWindow.updateText(_matchedDecks.Count + " Matching Deck(s) Found", Brushes.LightGreen);

                    return validDecks;
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    return null;
                }
            }
            else
                return null;
        }

        public void SaveMetaDeckStats()
        {
            if (_validGameMode)
                if (Core.Game.CurrentGameMode == GameMode.Ranked && Core.Game.CurrentFormat == Format.Standard)
                    XmlManager<List<Deck>>.Save(_deckFilename, _metaDecks);
        }

        public string SendDeckStats()
        {
            if (_validGameMode)
            {
                if ((DateTime.Now - _appConfig.lastUpload).TotalDays > 1)
                {
                    string url = "http://www.desi-radio.com/meta.php";

                    List<KeyValuePair<string, string>> deckStats = new List<KeyValuePair<string, string>>();
                    StringBuilder builder = new StringBuilder();


                    foreach (Deck d in _metaDecks.Where(x => x.Note != "0" ) )
                    {
                        builder.Append(d.DeckId.ToString()).Append(":").Append(d.Note).Append(',');
                    }

                    string result = builder.ToString().TrimEnd(',');

                    string postData = result;

                    string webpageContent = string.Empty;

                    try
                    {
                        byte[] byteArray = Encoding.UTF8.GetBytes(postData);

                        HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);

                        webRequest.ContentType = "application/x-www-form-urlencoded";
                        webRequest.ContentLength = byteArray.Length;
                        webRequest.Method = "POST";

                        using (Stream webpageStream = webRequest.GetRequestStream())
                        {
                            webpageStream.Write(byteArray, 0, byteArray.Length);
                        }

                        using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse())
                        {
                            using (StreamReader reader = new StreamReader(webResponse.GetResponseStream()))
                            {
                                webpageContent = reader.ReadToEnd();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //throw or return an appropriate response/exception
                        Log.Error(ex);
                    }

                    _appConfig.lastUpload = DateTime.Now;
                    _appConfig.Save();
                    return webpageContent;
                }

                return "";
            }
            else
            {
                return "";
            }
        }
    }

    public class trackCards
    {
        private static string statsDirectory = Path.Combine(Config.AppDataPath, "MetaDetector");
        private static string statsPath = Path.Combine(statsDirectory, "cardStats.xml");

        public string playerClass { get; set; }
        public int turn { get; set; }
        public string cardId { get; set; }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(statsDirectory))
                    Directory.CreateDirectory(statsDirectory);

                var serializer = new XmlSerializer(typeof(MyConfig));
                using (var writer = new StreamWriter(statsPath))
                    serializer.Serialize(writer, this);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }

    public class MyConfig
    {
        private static string configDirectory = Path.Combine(Config.AppDataPath, "MetaDetector");
        private static string configPath = Path.Combine(configDirectory, "metaConfig.xml");

        public string currentVersion { get; set; }
        public DateTime lastCheck { get; set; }
        public DateTime lastUpload { get; set; }

        public MyConfig()
        {

        }

        public MyConfig(string v, DateTime c, DateTime u)
        {
            this.currentVersion = v;
            this.lastCheck = c;
            this.lastUpload = u;
        }
        public static MyConfig Load()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var serializer = new XmlSerializer(typeof(MyConfig));
                    using (var reader = new StreamReader(configPath))
                        return (MyConfig)serializer.Deserialize(reader);
                }
                else
                {
                    return new MyConfig("1", DateTime.Now, DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(configDirectory))
                    Directory.CreateDirectory(configDirectory);

                var serializer = new XmlSerializer(typeof(MyConfig));
                using (var writer = new StreamWriter(configPath))
                    serializer.Serialize(writer, this);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }
}
