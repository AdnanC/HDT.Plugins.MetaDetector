using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;
using HDT.Plugins.MetaDetector.Logging;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Hearthstone;
using System.Windows.Navigation;

namespace HDT.Plugins.MetaDetector.Controls
{
    /// <summary>
    /// Interaction logic for OpDeckWindow.xaml
    /// </summary>
    public partial class OpDeckWindow
    {
        public OpDeckWindow()
        {
            InitializeComponent();

            lbDecks.DisplayMemberPath = "GetClass";
        }

        public void clearLists()
        {
            lvDeckList.ItemsSource = null;
            lbDecks.Items.Clear();
        }

        public void resetWindow(List<Deck> metaDecks)
        {
            lvDeckList.ItemsSource = null;
            lbDecks.Items.Clear();
            /*foreach (Deck deck in metaDecks.OrderBy(d => d.Class))
            {
                deck.Name = "Meta Deck";
                lbDecks.Items.Add(deck);
            }*/
            tbInformation.Text = "Waiting For Cards...";
            tbInformation.Foreground = Brushes.White;
            tbMetaRank.Text = "";
        }

        public void updateCardList(Deck deck)
        {
            lvDeckList.ItemsSource = deck.Cards;
            Helper.SortCardCollection(lvDeckList.Items, false);
        }

        public void updateCardsCount(int count)
        {
            tbCardsPlayed.Text = "Cards Revealed: " + count.ToString();
        }

        public void updateVersion(Version pluginVersion)
        {
            tbVersion.Text = "v" + pluginVersion.ToString();
        }

        public void newVersionAvailable()
        {
            tbWebsite.Visibility = System.Windows.Visibility.Hidden;
            tbVersion.Visibility = System.Windows.Visibility.Visible;
        }

        public void updateDeckList(List<Deck> metaDecks)
        {
            try
            {
                if (metaDecks.Count > 0)
                {
                    lbDecks.Items.Clear();

                    foreach (Deck deck in metaDecks.OrderByDescending(x => Convert.ToInt32(x.Note)).ThenBy(x => x.Class))
                    {
                        lbDecks.Items.Add(deck);
                    }

                    if (lbDecks.Items.Count > 0)
                    {
                        lbDecks.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }

        public Deck getSelectedDeck()
        {
            return (Deck)lbDecks.SelectedItem;
        }

        public void setSelectedDeck(Deck selectedDeck)
        {
            //lbDecks.SelectedItem = selectedDeck;
        }

        public void updateText(string message, Brush color)
        {
            tbInformation.Text = message;
            tbInformation.Foreground = color;
        }

        private void DeckWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Visibility = System.Windows.Visibility.Hidden;
        }

        private void lbDecks_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (lbDecks.SelectedItem != null)
            {
                lvDeckList.ItemsSource = ((Deck)lbDecks.SelectedItem).Cards;
                Hearthstone_Deck_Tracker.Helper.SortCardCollection(lvDeckList.Items, false);

                double rank = Convert.ToDouble(((Deck)lbDecks.SelectedItem).Note) / 100;
                tbMetaRank.Text = "Meta Rank: " + rank.ToString();
            }
        }

        private void lbDecks_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        private void btnSaveDeck_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (lbDecks.SelectedIndex >= 0)
            {
                Deck temp = (Deck)lbDecks.SelectedItem;

                temp.Name = "Meta Deck - " + temp.Class;

                Hearthstone_Deck_Tracker.API.Core.MainWindow.SetNewDeck(temp);
                Hearthstone_Deck_Tracker.API.Core.MainWindow.ActivateWindow();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(e.Uri.ToString());
            }
            catch (Exception ex)
            {
                MetaLog.Error(ex);
            }
        }
    }
}
