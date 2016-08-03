using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;
using System.Net;
using System.Xml.Serialization;
using System.Threading.Tasks;
using System.ComponentModel;
using Hearthstone_Deck_Tracker;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Enums;
using HDT.Plugins.MetaDetector.Logging;
using Hearthstone_Deck_Tracker.Hearthstone;
using HDT.Plugins.MetaDetector.Controls;
using System.Text;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;

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
        private trackCards _cardsPlayedOpponent;
        private trackCards _cardsPlayedPlayer;
        private static string _deckDirectory = Path.Combine(Config.AppDataPath, @"MetaDetector");
        private static string _deckFilename = Path.Combine(_deckDirectory, @"metaDecks.xml");
        private int _opponentCardCheck = 2;
        private int _opponentCardCount = 0;
        private int _opponentTurnCount = 0;
        private int _playerTurnCount = 0;
        private int _bestMetaRank = 0;
        private int _metaRank = 0;
        private Card _lastCardPlayed = null;
        //private Card _lastOpponentCard = null;
        internal bool _statsUpdated = false;
        private bool _closestMatchedDecks = false;
        Dictionary<int, CardInfo> _trackOpponentCards = new Dictionary<int, CardInfo>();
        Dictionary<int, CardInfo> _trackPlayerCards = new Dictionary<int, CardInfo>();

        public MetaDetector(OpDeckWindow mainWindow)
        {
            //_mainWindow = new OpDeckWindow();
            _mainWindow = mainWindow;
            _lastGuessDeck = new Deck();

            _metaDecks = new List<Deck>();
            _matchedDecks = new List<Deck>();
            _statsUpdated = false;

            _opponentCardsPlayed = new List<Card>();

            _cardsPlayedOpponent = new trackCards();
            _cardsPlayedPlayer = new trackCards();

            _appConfig = MyConfig.Load();
            _appConfig.Save();
            MetaLog.Initialize();

            MetaLog.Info("Meta Detector Initialized", "MetaDetector");
        }

        private void checkGameMode()
        {
            //if (Core.Game.CurrentGameMode == GameMode.All)
            _validGameMode = true;
            //else
            //_validGameMode = false;
        }

        internal void GameStart()
        {
            MetaLog.Info("Game Mode: " + Core.Game.CurrentGameMode, "GameStart");
            MetaLog.Info("Game Format: " + Core.Game.CurrentFormat, "GameStart");
            MetaLog.Info("Region: " + Core.Game.CurrentRegion, "GameStart");
            MetaLog.Info("Mode: " + Core.Game.CurrentMode, "GameStart");

            LoadMetaDecks();
            _cardsPlayedOpponent.Clear();
            _cardsPlayedPlayer.Clear();
            _trackOpponentCards.Clear();
            _trackPlayerCards.Clear();
            checkGameMode();

            if (_validGameMode)
            {
                _opponentCardCheck = 2;
                _opponentCardCount = 0;
                _opponentTurnCount = 0;
                _playerTurnCount = 0;
                _bestMetaRank = 0;
                _metaRank = 0;
                _closestMatchedDecks = false;
                _statsUpdated = false;

                //if (_mainWindow.Visibility == System.Windows.Visibility.Hidden || _mainWindow.Visibility == System.Windows.Visibility.Collapsed)
                //    _mainWindow.Show();

                _mainWindow.updateCardsCount(_opponentCardCount);
                _mainWindow.resetWindow(_metaDecks);

                MetaLog.Info("New Game Started. Waiting for opponent to play cards.", "GameStart");
            }

            
            if (Core.Game.Player.HasCoin && _opponentTurnCount == 0)
                _opponentTurnCount++;
        }

        internal void PlayerMulligan(Card c)
        {
            _trackPlayerCards.FirstOrDefault(x => x.Value.cardId == c.Id).Value.mulligan = true;

            MetaLog.Info("Player Mulliganed " + c.Name, "PlayerMulligan");
        }

        internal void OpponentMulligan()
        {
            
        }

        internal void TurnStart(ActivePlayer activePlayer)
        {
            if (ActivePlayer.Player == activePlayer)
            {
                _playerTurnCount++;
                updateOpponentCardsPlayed();
            }
            else
            {
                _opponentTurnCount++;
            }

            /*if (Core.Game.Player.HasCoin)
                _opponentTurnCount++;
            else
                _playerTurnCount++;*/

            try
            {
                checkGameMode();

                if (_validGameMode)
                {
                    if (ActivePlayer.Player == activePlayer)
                    {
                        updateDecks();
                    }
                }
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }

        public void PlayerDraw(Card c)
        {
            try
            {
                updatePlayerHandCards();
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }

        private void updatePlayerHandCards()
        {
            foreach (var e in Core.Game.Entities.Where(x => x.Value.CardId != null && x.Value.CardId != "" &&
                    !x.Value.IsHero && !x.Value.IsHeroPower && x.Value.IsInHand &&
                    x.Value.GetTag(GameTag.CONTROLLER) == Core.Game.Player.Id).ToList())
            {
                if (_trackPlayerCards.Where(x => x.Key == e.Value.Id).Count() > 0)
                {

                }
                else
                {
                    MetaLog.Info("Turn " + _playerTurnCount + ": Player Draws " + e.Value.LocalizedName);

                    if (e.Value.GetTag(GameTag.CREATOR) > 0)
                    {
                        _trackPlayerCards.Add(e.Value.Id, new CardInfo(e.Value.Info.Turn, e.Value.Info.Mulliganed, e.Value.CardId, -1, -1, -1, -1,
                            e.Value.Info.Created, Core.Game.Entities[e.Value.GetTag(GameTag.CREATOR)].CardId));
                    }
                    else
                    {
                        _trackPlayerCards.Add(e.Value.Id, new CardInfo(e.Value.Info.Turn, e.Value.Info.Mulliganed, e.Value.CardId, -1, -1, -1, -1,
                            e.Value.Info.Created, ""));
                    }
                }
            }
        }

        public void OpponentSecretTriggered(Card c)
        {
            try
            {
                foreach (Entity e in Core.Game.Entities.Where(x => x.Value.CardId != null && !x.Value.IsHeroPower
                && !x.Value.IsHero && x.Value.GetTag(GameTag.CONTROLLER) == Core.Game.Opponent.Id
                && x.Value.IsSecret).Select(x => x.Value))
                {
                    CardInfo temp;
                    if (_trackOpponentCards.TryGetValue(e.Id, out temp))
                    {
                        if (_trackOpponentCards[e.Id].cardId == "")
                        {
                            _trackOpponentCards[e.Id].cardId = e.CardId;
                            _trackOpponentCards[e.Id].turnCardPlayed = e.Info.Turn;
                            _trackOpponentCards[e.Id].mana = Core.Game.OpponentEntity.GetTag(GameTag.RESOURCES);
                            _trackOpponentCards[e.Id].manaoverload = Core.Game.OpponentEntity.GetTag(GameTag.OVERLOAD_OWED);

                            if (e.GetTag(GameTag.CREATOR) > 0)
                            {
                                _trackOpponentCards[e.Id].created = e.Info.Created;
                                _trackOpponentCards[e.Id].createdBy = Core.Game.Entities[e.GetTag(GameTag.CREATOR)].CardId;
                            }
                        }
                    }
                    else
                    {
                        if (e.GetTag(GameTag.CREATOR) > 0)
                        {
                            _trackOpponentCards.Add(e.Id, new CardInfo(-1, false, e.CardId, e.Info.Turn,
                            Core.Game.OpponentEntity.GetTag(GameTag.RESOURCES), Core.Game.OpponentEntity.GetTag(GameTag.OVERLOAD_OWED), -1,
                            e.Info.Created, Core.Game.Entities[e.GetTag(GameTag.CREATOR)].CardId));
                        }
                        else
                        {
                            _trackOpponentCards.Add(e.Id, new CardInfo(-1, false, e.CardId, e.Info.Turn,
                            Core.Game.OpponentEntity.GetTag(GameTag.RESOURCES), Core.Game.OpponentEntity.GetTag(GameTag.OVERLOAD_OWED), -1,
                            e.Info.Created, ""));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }

        public void OpponentDraw()
        {
            try
            {
                foreach (Entity e in Core.Game.Entities.Select(x => x.Value).Where(x => !x.IsHero
                && !x.IsHeroPower && x.GetTag(GameTag.CONTROLLER) == Core.Game.Opponent.Id))
                {
                    if (e.CardId == null || e.CardId == "")
                    {
                        if (_trackOpponentCards.Where(x => x.Key == e.Id).Count() > 0)
                        {
                            _trackOpponentCards[e.Id].turnInHand = e.Info.Turn;
                            _trackOpponentCards[e.Id].mulligan = e.Info.Mulliganed;
                            _trackOpponentCards[e.Id].created = e.Info.Created;

                            if (e.GetTag(GameTag.CREATOR) > 0)
                                _trackOpponentCards[e.Id].createdBy = Core.Game.Entities[e.GetTag(GameTag.CREATOR)].CardId;
                        }
                        else
                        {
                            if (e.GetTag(GameTag.CREATOR) > 0)
                            {
                                _trackOpponentCards.Add(e.Id, new CardInfo(e.Info.Turn, e.Info.Mulliganed, "", -1, -1, -1, -1,
                                e.Info.Created, Core.Game.Entities[e.GetTag(GameTag.CREATOR)].CardId));
                            }
                            else
                            {
                                _trackOpponentCards.Add(e.Id, new CardInfo(e.Info.Turn, e.Info.Mulliganed, "", -1, -1, -1, -1,
                                e.Info.Created, ""));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }

        public void PlayerPlay(Card cardPlayed)
        {
            try
            {
                _lastCardPlayed = cardPlayed;

                updatePlayerHandCards();
                updatePlayerBoardEntities();

                MetaLog.Info("Turn " + _playerTurnCount + ": Player Played - " + cardPlayed.Name, "PlayerPlay");
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }

        private void updatePlayerBoardEntities()
        {
            foreach (Entity e in Core.Game.Player.Board.Where(x => !x.IsHero && !x.IsHeroPower))
            {
                CardInfo temp;
                if (_trackPlayerCards.TryGetValue(e.Id, out temp))
                {
                    _trackPlayerCards[e.Id].turnCardPlayed = e.Info.Turn;

                    if (e.GetTag(GameTag.CREATOR) > 0)
                    {
                        _trackPlayerCards[e.Id].created = e.Info.Created;
                        _trackPlayerCards[e.Id].createdBy = Core.Game.Entities[e.GetTag(GameTag.CREATOR)].CardId;
                    }
                }
                else
                {
                    if (e.GetTag(GameTag.CREATOR) > 0)
                    {
                        _trackPlayerCards.Add(e.Id, new CardInfo(-1, false, e.CardId, e.Info.Turn, -1, -1, -1,
                            e.Info.Created, Core.Game.Entities[e.GetTag(GameTag.CREATOR)].CardId));
                    }
                    else
                    {
                        _trackPlayerCards.Add(e.Id, new CardInfo(-1, false, e.CardId, e.Info.Turn, -1, -1, -1,
                            e.Info.Created, ""));
                    }
                }
            }
        }

        public void OpponentCreateInPlay(Card cardCreated)
        {
            updateOpponentCardsPlayed();
            /*if (cardCreated.Type == "Minion")
            {
                int turn = Core.Game.OpponentEntity.GetTag(GameTag.TURN);
                _cardsPlayedOpponent.Add(cardCreated.Id, turn, true, -1, false, -1, cardCreated.Name, Core.Game.OpponentEntity.GetTag(GameTag.RESOURCES), Core.Game.OpponentEntity.GetTag(GameTag.OVERLOAD_OWED), "Opponent", _lastCardPlayed.Id);
                MetaLog.Info(cardCreated.Name + " was created by " + _lastCardPlayed.Name, "OpponentCreateInPlay");
            }*/
        }

        public void OpponentCreateInDeck(Card cardCreated)
        {
            /*
            int turn = Core.Game.OpponentEntity.GetTag(GameTag.TURN);
            _cardsPlayedOpponent.Add(cardCreated.Id, turn, true, -1, false, -1, cardCreated.Name, Core.Game.OpponentEntity.GetTag(GameTag.RESOURCES), Core.Game.OpponentEntity.GetTag(GameTag.OVERLOAD_OWED), "Opponent", _lastCardPlayed.Id);
            MetaLog.Info(cardCreated.Name + " was created by " + _lastCardPlayed.Name, "OpponentCreateInDeck");
        */
        }

        public void PlayerCreateInPlay(Card cardCreated)
        {
            updatePlayerBoardEntities();
            /*if (cardCreated.Type == "Minion")
            {
                int turn = Core.Game.PlayerEntity.GetTag(GameTag.TURN);
                _cardsPlayedOpponent.Add(cardCreated.Id, turn, true, -1, false, -1, cardCreated.Name, Core.Game.PlayerEntity.GetTag(GameTag.RESOURCES), Core.Game.PlayerEntity.GetTag(GameTag.OVERLOAD_OWED), "Player", _lastCardPlayed.Id);

                MetaLog.Info(cardCreated.Name + " was created by " + _lastCardPlayed.Name, "PlayerCreateInPlay");
            }*/


        }

        public void PlayerCreateInDeck(Card cardCreated)
        {
            /*
            int turn = Core.Game.PlayerEntity.GetTag(GameTag.TURN);
            _cardsPlayedOpponent.Add(cardCreated.Id, turn, true, -1, false, -1, cardCreated.Name, Core.Game.PlayerEntity.GetTag(GameTag.RESOURCES), Core.Game.PlayerEntity.GetTag(GameTag.OVERLOAD_OWED), "Player", _lastCardPlayed.Id);

            MetaLog.Info(cardCreated.Name + " was created by " + _lastCardPlayed.Name, "PlayerCreateInDeck");
        */

        }

        public void OpponentPlay(Card cardPlayed)
        {
            try
            {
                _lastCardPlayed = cardPlayed;

                if (_validGameMode)
                {
                    //_opponentCardsPlayed.Add(cardPlayed);

                    if (cardPlayed.Id != "GAME_005") //ignore the coin
                    {
                        _opponentCardCount++;
                        updateDecks();
                    }
                }

                updateOpponentCardsPlayed();
                MetaLog.Info("Turn " + _opponentTurnCount + ": Opponent Played - " + cardPlayed.Name, "OpponentPlay");
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }

        private void updateOpponentCardsPlayed()
        {
            try
            {
                foreach (Entity e in Core.Game.Entities.Select(v => v.Value).Where( x => x.CardId != null && !x.IsHeroPower
                         && !x.IsHero && x.GetTag(GameTag.CONTROLLER) == Core.Game.Opponent.Id && x.IsInPlay)
                    )
/*                foreach (Entity e in Core.Game.Entities.Where(x => x.Value.CardId != null && !x.Value.IsHeroPower
                && !x.Value.IsHero && x.Value.GetTag(GameTag.CONTROLLER) == Core.Game.Opponent.Id
                && x.Value.IsInPlay).Select(x => x.Value))*/
                {
                    CardInfo temp;
                    if (_trackOpponentCards.TryGetValue(e.Id, out temp))
                    {
                        if (_trackOpponentCards[e.Id].cardId == "")
                        {
                            _trackOpponentCards[e.Id].cardId = e.CardId;
                            _trackOpponentCards[e.Id].turnCardPlayed = e.Info.Turn;
                            _trackOpponentCards[e.Id].mana = Core.Game.OpponentEntity.GetTag(GameTag.RESOURCES);
                            _trackOpponentCards[e.Id].manaoverload = Core.Game.OpponentEntity.GetTag(GameTag.OVERLOAD_OWED);

                            if (e.GetTag(GameTag.CREATOR) > 0)
                            {
                                _trackOpponentCards[e.Id].created = e.Info.Created;
                                _trackOpponentCards[e.Id].createdBy = Core.Game.Entities[e.GetTag(GameTag.CREATOR)].CardId;
                            }
                        }
                    }
                    else
                    {
                        if (e.GetTag(GameTag.CREATOR) > 0)
                        {
                            _trackOpponentCards.Add(e.Id, new CardInfo(-1, false, e.CardId, e.Info.Turn,
                            Core.Game.OpponentEntity.GetTag(GameTag.RESOURCES), Core.Game.OpponentEntity.GetTag(GameTag.OVERLOAD_OWED), -1,
                            e.Info.Created, Core.Game.Entities[e.GetTag(GameTag.CREATOR)].CardId));
                        }
                        else
                        {
                            _trackOpponentCards.Add(e.Id, new CardInfo(-1, false, e.CardId, e.Info.Turn,
                            Core.Game.OpponentEntity.GetTag(GameTag.RESOURCES), Core.Game.OpponentEntity.GetTag(GameTag.OVERLOAD_OWED), -1,
                            e.Info.Created, ""));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //MetaLog.Error(ex);
            }

        }

        public void OpponentHeroPower()
        {
            _cardsPlayedOpponent.Add("HERO_POWER", Convert.ToInt16(Math.Ceiling(Core.Game.GameEntity.GetTag(GameTag.TURN) / 2.0)), false, -1, false, -1, "Hero Power", Core.Game.OpponentEntity.GetTag(GameTag.RESOURCES), Core.Game.OpponentEntity.GetTag(GameTag.OVERLOAD_OWED), "Opponent");
            MetaLog.Info("Turn " + _opponentTurnCount + ": Opponent Hero Power", "OpponentHeroPower");
        }

        public void PlayerHeroPower()
        {
            if (Core.Game.Opponent.HasCoin && _playerTurnCount == 0)
                _playerTurnCount++;

            _cardsPlayedOpponent.Add("HERO_POWER", Convert.ToInt16(Math.Ceiling(Core.Game.GameEntity.GetTag(GameTag.TURN)/2.0)), false, -1, false, -1, "Hero Power", Core.Game.PlayerEntity.GetTag(GameTag.RESOURCES), Core.Game.PlayerEntity.GetTag(GameTag.OVERLOAD_OWED), "Player");
            MetaLog.Info("Turn " + _playerTurnCount + ": Player Hero Power", "PlayerHeroPower");
        }

        public void OpponentPlayToGraveyard(Card c)
        {
            MetaLog.Info("Turn " + _opponentTurnCount + ": Minion Dead - " + c.Name, "OpponentPlayToGraveyard");
        }

        public async void GameEnd()
        {
            //string matchedDeck = "";

            try
            {
                /*if (_metaDecks != null)
                {
                    if (_metaDecks.Count < 10)
                        matchedDeck = _metaDecks.FirstOrDefault().DeckId.ToString();
                }*/
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }

            if (_validGameMode)
                try
                {
                    MetaLog.Info("Matched Decks: " + _matchedDecks.Count);

                    if (_matchedDecks.Count <= 30)
                    {
                        _metaRank = _opponentCardCount;
                    }

                    if (_matchedDecks.Count <= 10 & !_closestMatchedDecks)
                    {
                        _bestMetaRank = _opponentTurnCount;
                    }

                    if (_matchedDecks.Count <= 5)
                    {
                        _bestMetaRank = _opponentTurnCount;
                    }

                    if (Core.Game.CurrentFormat == Format.Standard)
                        if (_metaRank > 0 || _bestMetaRank > 0)
                        {
                            MetaLog.Info("Updating ranks (" + _metaRank + "+" + _bestMetaRank + ") for " + _matchedDecks.Count + " deck(s).", "GameEnd");

                            foreach (Deck d in _matchedDecks)
                            {
                                _metaDecks.Find(x => x.DeckId == d.DeckId).Note = (Convert.ToInt32(_metaDecks.Find(x => x.DeckId == d.DeckId).Note) + _metaRank + _bestMetaRank).ToString();
                            }

                            SaveMetaDeckStats();
                            //await sendMetaRanks();
                        }

                    _matchedDecks = new List<Deck>(_metaDecks);

                    _mainWindow.updateText("Waiting for new Game...", Brushes.White);
                    MetaLog.Info("Game Ended. Waiting for new Game", "GameEnd");
                }
                catch (Exception ex)
                {
                    MetaLog.Error(ex);
                }

            //get all cards played / died in the game
            try
            {
                foreach (var x in Core.Game.Opponent.Graveyard.Where(x => !x.IsHero && !x.IsHeroPower && x.CardId != ""))
                {
                    CardInfo temp;
                    if (_trackOpponentCards.TryGetValue(x.Id, out temp))
                    {
                        _trackOpponentCards[x.Id].turnCardDied = x.Info.Turn;
                    }
                }

                foreach (var x in Core.Game.Player.Graveyard.Where(x => !x.IsHero && !x.IsHeroPower && x.CardId != ""))
                {
                    CardInfo temp;
                    if (_trackPlayerCards.TryGetValue(x.Id, out temp))
                    {
                        _trackPlayerCards[x.Id].turnCardDied = x.Info.Turn;
                    }
                }

                //_cardsPlayed.Clear();
                foreach (CardInfo x in _trackOpponentCards.Values.Where(x => x.cardId != "" && x.cardId != null).OrderBy(x => x.turnCardPlayed).ToList())
                {
                    _cardsPlayedOpponent.Add(x.cardId, x.turnCardPlayed, x.created, x.turnInHand, x.mulligan,
                        x.turnCardDied, Database.GetCardFromId(x.cardId).Name, x.mana, x.manaoverload, "Opponent", x.createdBy);
                }

                foreach (CardInfo x in _trackPlayerCards.Values.Where(x => x.cardId != "" && x.cardId != null).OrderBy(x => x.turnCardPlayed).ToList())
                {
                    _cardsPlayedOpponent.Add(x.cardId, x.turnCardPlayed, x.created, x.turnInHand, x.mulligan,
                        x.turnCardDied, Database.GetCardFromId(x.cardId).Name, x.mana, x.manaoverload, "Player", x.createdBy);
                }

                if (Core.Game.CurrentGameStats.Result == GameResult.Win)
                    _cardsPlayedOpponent.setPlayerWin();
                else if (Core.Game.CurrentGameStats.Result == GameResult.Loss)
                    _cardsPlayedOpponent.setOpponentWin();

                _cardsPlayedOpponent.Save();
                await sendCardStats();
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }

        internal void updateDecks()
        {
            if (_validGameMode)
                try
                {
                    List<Deck> displayDecks = new List<Deck>();

                    if (_opponentCardCount > 0)
                    {
                        displayDecks = matchMetaDeck();

                        _mainWindow.updateCardsCount(Core.Game.Opponent.OpponentCardList.Where(x => !x.IsCreated).Count());

                        if (_matchedDecks.Count == 0)
                        {
                            _mainWindow.updateText("No Decks Found.", Brushes.IndianRed);
                            _statsUpdated = false;
                            return;
                        }
                        /*else if (_matchedDecks.Count > 0 && !_closestMatchedDecks)
                        {
                            //_mainWindow.updateCardsCount(Core.Game.Opponent.RevealedEntities.Where(x => (x.IsInDeck || x.IsMinion || x.IsSpell || x.IsWeapon) && !x.Info.Created && !x.Info.Stolen).Count());
                            if (_opponentCardCount > _opponentCardCheck)
                            {
                                _opponentCardCheck = _opponentCardCount;

                                if (_matchedDecks.Count <= 30)
                                {
                                    _metaRank = _opponentCardCount;
                                }

                                if (_matchedDecks.Count <= 10)
                                {
                                    _bestMetaRank = _opponentTurnCount;
                                }
                            }

                        }*/

                        if (displayDecks != null)
                            _mainWindow.updateDeckList(displayDecks);
                    }
                }
                catch (Exception ex)
                {
                    MetaLog.Error(ex);
                }
        }

        internal bool checkNewVersion()
        {
            try
            {
                string currentVersion = _appConfig.currentVersion;
                DateTime lastCheck = _appConfig.lastCheck;

                if ((DateTime.Now - lastCheck).TotalDays > 3)
                {
                    MetaLog.Info("Checking for new version of Meta File", "checkNewVersion");
                    WebClient client = new WebClient();
                    String versionNumber = client.DownloadString("http://metastats.net/metadetector/metaversion.php");

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
            catch (Exception ex)
            {
                MetaLog.Error(ex);
                return false;
            }
        }

        internal void LoadMetaDecks()
        {
            try
            {
                if (checkNewVersion())
                    return;

                if (File.Exists(_deckFilename))
                {
                    _metaDecks = XmlManager<List<Deck>>.Load(_deckFilename);
                    _matchedDecks = new List<Deck>(_metaDecks);

                    if (_matchedDecks.Count == 0)
                        DownloadMetaFile();

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
                MetaLog.Error(ex);
            }
        }

        internal static void DecompressFile(FileInfo fileToDecompress)
        {
            try
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
            catch (Exception ex)
            {
                MetaLog.Info("Unable to decompress Meta file", "DecompressFile");
                MetaLog.Error(ex);
            }
        }

        internal void DownloadMetaFile()
        {
            try
            {
                MetaLog.Info("Downloing Meta File");
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
                MetaLog.Info("Error while downloing Meta File");
                MetaLog.Error(ex);
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
                MetaLog.Info("Meta File Download Complete");
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
                MetaLog.Error(ex);
            }
        }

        internal void LoadMetaDeckfromZip()
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
                MetaLog.Error(ex);
            }
        }

        internal List<Deck> matchMetaDeck()
        {
            if (_validGameMode)
            {
                try
                {
                    var validDecks = _metaDecks.Where(x => x.Class == Core.Game.Opponent.Class).ToList();

                    //var cardEntites = Core.Game.Opponent.RevealedEntities.Where(x => (x.IsMinion || x.IsSpell || x.IsWeapon) && !x.Info.Created && !x.Info.Stolen).GroupBy(x => x.CardId).ToList();
                    var cardEntites = Core.Game.Opponent.OpponentCardList.Where(x => !x.IsCreated).ToList();

                    if (validDecks.Count > 1 && cardEntites != null)
                        validDecks = validDecks.Where(x => cardEntites.All(ce => x.GetSelectedDeckVersion().Cards.Any(c => c.Id == ce.Id && c.Count >= ce.Count))).ToList();


                    if (validDecks.Count == 0)
                    {
                        _mainWindow.updateText("No Match Found. Showing Closest Decks", Brushes.YellowGreen);
                        //MetaLog.Info("No Match Found. Showing Closest Decks");
                        _closestMatchedDecks = true;

                        if (_matchedDecks.Count <= 10 && _opponentTurnCount > 6)
                            return _matchedDecks;

                        validDecks = _metaDecks.Where(x => x.Class == Core.Game.Opponent.Class).ToList();

                        _matchedDecks = new List<Deck>(validDecks);

                        validDecks.Clear();
                        int lastCount = 0;
                        foreach (Deck d in _matchedDecks.OrderBy(x => Convert.ToInt16(x.Note)))
                        {
                            int count = d.Cards.Intersect(Core.Game.Opponent.PlayerCardList.Where(x => !x.IsCreated)).ToList().Count();
                            if (count > 0)
                            {
                                if (count >= lastCount)
                                {
                                    validDecks.Insert(0, d);
                                    lastCount = count;
                                }
                                else
                                {
                                    validDecks.Add(d);
                                }
                            }
                        }
                        _matchedDecks = new List<Deck>(validDecks);

                        if (validDecks.Count > 10)
                        {
                            validDecks = validDecks.Take(10).ToList();
                        }

                        return validDecks;
                    }

                    _matchedDecks = new List<Deck>(validDecks);

                    if (validDecks.Count > 20)
                        validDecks = validDecks.Where(x => cardEntites.Any(ce => x.GetSelectedDeckVersion().Cards.Any(c => c.Id == ce.Id))).Take(20).ToList();

                    _mainWindow.updateText(_matchedDecks.Count + " Matching Deck(s) Found", Brushes.LightGreen);

                    return validDecks;
                }
                catch (Exception ex)
                {
                    MetaLog.Error(ex);
                    return null;
                }
            }
            else
                return null;
        }

        public void SaveMetaDeckStats()
        {
            MetaLog.Info("Saving Meta Ranks to file", "SaveMetaDeckStats");
            XmlManager<List<Deck>>.Save(_deckFilename, _metaDecks);
        }

        internal async Task<string> sendCardStats()
        {
            try
            {
                if (_validGameMode)
                {
                    {
                        MetaLog.Info("Uploading Card Stats...", "sendRequest");

                        string url = "http://metastats.net/metadetector/cards.php?v=2";


                        string postData = _cardsPlayedOpponent.GetCardStats();

                        if (postData != "")
                        {

                            WebClient client = new WebClient();
                            byte[] data = Encoding.UTF8.GetBytes(postData);
                            Uri uri = new Uri(url);
                            var response = Encoding.UTF8.GetString(await client.UploadDataTaskAsync(uri, "POST", data));

                            _appConfig.lastUpload = DateTime.Now;
                            _appConfig.Save();

                            MetaLog.Info("Uploading Card Stats Done", "sendRequest");

                            /*
                            client.Headers.Add("Content-Type", "binary/octet-stream");

                            byte[] result = client.UploadFile("http://metastats.net/metadetector/upload.php", "POST",
                                                              trackCards.statsPath);

                            string s = Encoding.UTF8.GetString(result, 0, result.Length);
                            */

                            return response;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
                return null;
            }
        }

        internal async Task<string> sendMetaRanks()
        {
            try
            {
                if (_validGameMode)
                {
                    if ((DateTime.Now - _appConfig.lastUpload).TotalDays > 1)
                    {
                        MetaLog.Info("Uploading Meta Ranks...", "sendRequest");

                        string url = "http://metastats.net/metadetector/meta.php";

                        StringBuilder builder = new StringBuilder();
                        foreach (Deck d in _metaDecks.Where(x => x.Note != "0"))
                        {
                            builder.Append(d.DeckId.ToString()).Append(":").Append(d.Note).Append(',');
                        }

                        string postData = builder.ToString().TrimEnd(',');

                        WebClient client = new WebClient();
                        byte[] data = Encoding.UTF8.GetBytes(postData);
                        Uri uri = new Uri(url);
                        var response = Encoding.UTF8.GetString(await client.UploadDataTaskAsync(uri, "POST", data));

                        _appConfig.lastUpload = DateTime.Now;
                        _appConfig.Save();

                        MetaLog.Info("Uploading Meta Ranks Done", "sendRequest");
                        return response;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
                return null;
            }
        }

        internal string SendDeckStats()
        {
            if (_validGameMode)
            {
                if ((DateTime.Now - _appConfig.lastUpload).TotalDays > 1)
                {
                    string url = "http://metastats.net/metadetector/meta.php";

                    List<KeyValuePair<string, string>> deckStats = new List<KeyValuePair<string, string>>();
                    StringBuilder builder = new StringBuilder();


                    foreach (Deck d in _metaDecks.Where(x => x.Note != "0"))
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
                        MetaLog.Error(ex);
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

    public class CardInfo
    {
        public string cardId;
        public int turnCardPlayed;
        public int turnInHand;
        public int turnCardDied;
        public bool mulligan;
        public bool created;
        public string createdBy;
        public int mana;
        public int manaoverload;

        public CardInfo(int InHand, bool mul = false, string scardId = "",
            int nturnplayed = -1, int nmana = -1, int nmanaoverload = -1, int nturndied = -1, bool bCreated = false, string sCreatedBy = "")
        {
            this.mulligan = mul;
            this.turnInHand = InHand;
            this.cardId = scardId;
            this.turnCardPlayed = nturnplayed;
            this.mana = nmana;
            this.manaoverload = nmanaoverload;
            this.turnCardDied = nturndied;
            this.created = bCreated;
            this.createdBy = sCreatedBy;
        }
    }

    public class trackCards
    {
        private static string statsDirectory = Path.Combine(Config.AppDataPath, "MetaDetector");
        public static string statsPath = Path.Combine(statsDirectory, "cardStats.xml");

        public string gameId { get; set; }
        public string playerClass { get; set; }
        public string opponentClass { get; set; }
        public string gameFormat { get; set; }
        public string gameMode { get; set; }
        public string rankString { get; set; }
        public string region { get; set; }
        public int rankedSeasonId { get; set; }
        public int opponentRank { get; set; }
        public int opponentLegendRank { get; set; }
        public bool opponentCoin { get; set; }
        public int playerRank { get; set; }
        public int playerLegendRank { get; set; }
        public bool opponentWin { get; set; }
        public bool playerWin { get; set; }
        public string cardId { get; set; }
        public string cardName { get; set; }
        public int turn { get; set; }
        public int turnDrawn { get; set; }
        public int turnToGraveyard { get; set; }
        public bool mulligan { get; set; }
        public int mana { get; set; }
        public int manaOverload { get; set; }
        public bool isCreated { get; set; }
        public string createdBy { get; set; }
        public string activePlayer { get; set; }

        private List<trackCards> _cardsPlayed = new List<trackCards>();

        public trackCards()
        {
            this.turnDrawn = -1;
            this.turnToGraveyard = -1;
            this.turn = -1;
            this.mana = -1;
            this.manaOverload = -1;
            this.mulligan = false;
        }

        public void Add(string cardId, int nturn, bool isCreated,
            int nturnInHand, bool bmulligan,
            int nturnCardDied, string sName = "", int nMana = -1,
            int nManaOverload = -1, string sActivePlayer = "Opponent", string createdBy = "")
        {
            trackCards temp = new trackCards();

            var standard = Core.Game.CurrentFormat == Format.Standard;

            temp.gameId = null;
            temp.playerClass = Core.Game.CurrentGameStats.PlayerHero;
            temp.opponentClass = Core.Game.CurrentGameStats.OpponentHero;
            temp.gameFormat = Core.Game.CurrentFormat.ToString();
            temp.gameMode = Core.Game.CurrentGameMode.ToString();
            temp.opponentRank = standard ? Core.Game.MatchInfo.OpposingPlayer.StandardRank : Core.Game.MatchInfo.OpposingPlayer.WildRank;
            temp.opponentLegendRank = standard ? Core.Game.MatchInfo.OpposingPlayer.StandardLegendRank : Core.Game.MatchInfo.OpposingPlayer.WildLegendRank;
            temp.playerLegendRank = standard ? Core.Game.MatchInfo.LocalPlayer.StandardLegendRank : Core.Game.MatchInfo.LocalPlayer.WildLegendRank;
            temp.playerRank = standard ? Core.Game.MatchInfo.LocalPlayer.StandardRank : Core.Game.MatchInfo.LocalPlayer.WildRank;
            //temp.region = Core.Game.CurrentGameStats.RegionString;
            temp.region = Core.Game.CurrentRegion.ToString();
            temp.opponentCoin = Core.Game.Opponent.HasCoin;
            temp.rankedSeasonId = Core.Game.MatchInfo.RankedSeasonId;
            temp.opponentWin = false;
            temp.playerWin = false;
            temp.isCreated = isCreated;
            temp.createdBy = createdBy;
            temp.turnDrawn = nturnInHand;
            temp.mulligan = bmulligan;
            temp.turnToGraveyard = nturnCardDied;
            temp.turn = nturn;
            temp.cardId = cardId;
            temp.cardName = sName;
            temp.mana = nMana;
            temp.manaOverload = nManaOverload;
            temp.activePlayer = sActivePlayer;

            _cardsPlayed.Add(temp);
        }

        public void Clear()
        {
            _cardsPlayed.Clear();
        }

        public string GetCardStats()
        {
            var serializer = new XmlSerializer(typeof(List<trackCards>));

            using (StringWriter textWriter = new StringWriter())
            {
                serializer.Serialize(textWriter, _cardsPlayed);
                return textWriter.ToString();
            }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(statsDirectory))
                    Directory.CreateDirectory(statsDirectory);

                var serializer = new XmlSerializer(typeof(List<trackCards>));
                using (var writer = new StreamWriter(statsPath))
                    serializer.Serialize(writer, _cardsPlayed);
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }

        public void setPlayerWin()
        {
            foreach (trackCards c in _cardsPlayed)
            {
                c.playerWin = true;
            }
        }

        public void setOpponentWin()
        {
            foreach (trackCards c in _cardsPlayed)
            {
                c.opponentWin = true;
            }
        }

        public void setTurnInHand(Dictionary<int, CardInfo> cardsTracked)
        {
            foreach (var c in cardsTracked.Where(x => x.Value.cardId != null))
            {
                var t = (trackCards)_cardsPlayed.Where(x => x.turn == c.Value.turnCardPlayed && x.cardId == c.Value.cardId);
                t.mulligan = c.Value.mulligan;
                t.turnDrawn = c.Value.turnInHand;
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
                MetaLog.Error(ex);
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
                MetaLog.Error(ex);
            }
        }
    }
}
