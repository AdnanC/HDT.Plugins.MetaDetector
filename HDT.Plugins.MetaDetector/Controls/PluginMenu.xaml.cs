using System.Windows;

namespace HDT.Plugins.MetaDetector.Controls
{
    /// <summary>
    /// Interaction logic for PluginMenu.xaml
    /// </summary>
    public partial class PluginMenu
    {
        private OpDeckWindow _mainWindow;
        public PluginMenu(OpDeckWindow mainWindow)
        {
            InitializeComponent();

            _mainWindow = mainWindow;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.Show();
        }
    }
}
