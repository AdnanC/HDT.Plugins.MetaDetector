using Hearthstone_Deck_Tracker.Hearthstone;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Animation;

namespace HDT.Plugins.MetaDetector.Controls
{
    /// <summary>
    /// Interaction logic for AnimatedCard.xaml
    /// </summary>
    public partial class AnimatedCard
    {
        public AnimatedCard(Card card)
        {
            InitializeComponent();
            DataContext = card;
        }

        public Card Card => (Card) DataContext;

        public async Task FadeIn(bool fadeIn)
        {
            //Card.Update();

            await RunStoryBoard("StoryboardFadeIn");            
        }

        public async Task FadeOut(bool highlight)
        {
            await RunStoryBoard("StoryboardUpdate");

            //Card.Update();

            await RunStoryBoard("StoryboardFadeOut");
        }

        public async Task Update(bool highlight)
        {
            await RunStoryBoard("StoryboardUpdate");
            //Card.Update();
        }

        private readonly List<string> _runningStoryBoards = new List<string>();
        public async Task RunStoryBoard(string key)
        {
            if (_runningStoryBoards.Contains(key))
                return;
            _runningStoryBoards.Add(key);
            var sb = (Storyboard)FindResource(key);
            sb.Begin();
            await Task.Delay(sb.Duration.TimeSpan);
            _runningStoryBoards.Remove(key);
        }
    }
}
