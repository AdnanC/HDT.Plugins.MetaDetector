using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;
using System.Net;
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

namespace HDT.Plugins.MetaDetector
{
    public class MetaDetector
    {
        private OpDeckWindow _mainWindow;
        private Deck _lastGuessDeck;

        private List<Deck> _metaDecks;
        private List<Deck> _matchedDecks;
        private static string _deckDirectory = Path.Combine(Config.AppDataPath, @"MetaDetector");
        private static string _deckFilename = Path.Combine(_deckDirectory, @"metaDecks.xml");
        private int _opponentCardCheck = 2;
        private int _opponentCardCount = 0;
        internal bool _statsUpdated = false;

        public MetaDetector(OpDeckWindow mainWindow)
        {
            _mainWindow = new OpDeckWindow();
            _mainWindow = mainWindow;
            _lastGuessDeck = new Deck();

            _metaDecks = new List<Deck>();
            _matchedDecks = new List<Deck>();
            _statsUpdated = false;
            _opponentCardCheck = 2;

            LoadMetaDecks();
        }

        internal void TurnStart(ActivePlayer activePlayer)
        {
            if (ActivePlayer.Player == activePlayer)
            {
                updateDecks();
            }
        }

        internal void OpponentPlay(Card cardPlayed)
        {
            _opponentCardCount++;
            updateDecks();
        }

        internal void GameStart()
        {
            _opponentCardCheck = 2;
            _opponentCardCount = 0;
            _mainWindow.updateCardsCount(_opponentCardCount);
            _mainWindow.resetWindow(_metaDecks);
        }

        internal void GameEnd()
        {
            try
            {
                if (_statsUpdated)
                    SaveMetaDeckStats();
                _matchedDecks = _metaDecks;

                _mainWindow.updateText("Waiting...", Brushes.White);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void updateDecks()
        {
            try
            {
                _matchedDecks = matchMetaDeck();

                if (_matchedDecks.Count == 0 || _matchedDecks == null)
                {
                    _mainWindow.updateText("No Decks Found", Brushes.PaleVioletRed);
                    //_mainWindow.clearLists();
                    return;
                }
                else if (_matchedDecks.Count > 0)
                {
                    _mainWindow.updateCardsCount(_opponentCardCount);

                    if (_opponentCardCount > _opponentCardCheck)
                    {
                        int count = _opponentCardCount - _opponentCardCheck;

                        _opponentCardCheck = _opponentCardCount;

                        foreach (Deck d in _matchedDecks)
                        {
                            _metaDecks.Find(x => x.DeckId == d.DeckId).Note = (Convert.ToInt32(_metaDecks.Find(x => x.DeckId == d.DeckId).Note) + count).ToString();
                        }

                        _statsUpdated = true;
                    }
                }

                _mainWindow.updateText(_matchedDecks.Count + " Matching Deck(s) Found", Brushes.LimeGreen);
                _mainWindow.updateDeckList(_matchedDecks);

                //if (_statsUpdated)
                //SaveMetaDeckStats();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public void addMeta(string url)
        {
            AddMetaDeck(url);
        }

        void LoadMetaDecks()
        {
            try
            {
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

        private void DownloadMetaFile()
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                    wc.DownloadFileCompleted += (wc_DownloadFileCompleted);
                    wc.DownloadFileAsync(new Uri("http://dbbxdlqej7c64.cloudfront.net/metaDecks.txt"), _deckFilename);
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

                _mainWindow.updateText("Meta File Downloaded", Brushes.GreenYellow);
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

        public async void AddMetaDeck(string url)
        {
            try
            {
                var deck = await Hearthstone_Deck_Tracker.Importing.DeckImporter.Import(url);

                if (deck != null)
                {
                    Deck newMetaDeck = deck;
                    if (_metaDecks.Where(d => d.Name == newMetaDeck.Name).Count() == 0)
                    {
                        _metaDecks.Add(newMetaDeck);
                        XmlManager<List<Deck>>.Save(_deckFilename, _metaDecks);
                        _mainWindow.updateText("Deck Added " + newMetaDeck.Name, Brushes.GreenYellow);
                    }
                    else
                    {
                        _mainWindow.updateText("Deck Already Exists", Brushes.PaleVioletRed);
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
            try
            {
                var validDecks = _metaDecks.Where(x => x.Class == Core.Game.Opponent.Class).ToList();


                var cardEntites = Core.Game.Opponent.RevealedEntities.Where(x => (x.IsMinion || x.IsSpell || x.IsWeapon) && !x.Info.Created && !x.Info.Stolen).GroupBy(x => x.CardId).ToList();

                if (validDecks.Count > 1 && cardEntites != null)
                    validDecks = validDecks.Where(x => cardEntites.All(ce => x.GetSelectedDeckVersion().Cards.Any(c => c.Id == ce.Key && c.Count >= ce.Count()))).ToList();

                return validDecks;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        public void SaveMetaDeckStats()
        {
            if (Core.Game.CurrentGameMode == GameMode.Ranked && Core.Game.CurrentFormat == Format.Standard)
                XmlManager<List<Deck>>.Save(_deckFilename, _metaDecks);
        }

        public string SendDeckStats()
        {
            string url = "http://www.desi-radio.com/meta.php";

            List<KeyValuePair<string, string>> deckStats = new List<KeyValuePair<string, string>>();

            foreach (Deck d in _metaDecks)
            {
                deckStats.Add(new KeyValuePair<string, string>(d.DeckId.ToString(), d.Note));
            }
            //var json = new JavaScriptSerializer().Serialize(deckStats);


            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in deckStats)
            {
                builder.Append(pair.Key).Append(":").Append(pair.Value).Append(',');
            }
            string result = builder.ToString().TrimEnd(',');

            //string postData = json;
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

            return webpageContent;
        }
    }


}
