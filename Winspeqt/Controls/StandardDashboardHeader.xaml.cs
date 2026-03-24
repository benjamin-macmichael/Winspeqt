using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Winspeqt.Controls
{
    public sealed partial class StandardDashboardHeader : UserControl
    {
        public StandardDashboardHeader()
        {
            this.InitializeComponent();
        }

        public string IconGlyph
        {
            get => (string)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(StandardDashboardHeader), new PropertyMetadata(string.Empty));

        public Brush? IconBrush
        {
            get => (Brush?)GetValue(IconBrushProperty);
            set => SetValue(IconBrushProperty, value);
        }

        public static readonly DependencyProperty IconBrushProperty =
            DependencyProperty.Register(nameof(IconBrush), typeof(Brush), typeof(StandardDashboardHeader), new PropertyMetadata(null));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(StandardDashboardHeader), new PropertyMetadata(string.Empty));

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(StandardDashboardHeader), new PropertyMetadata(string.Empty));

        public event RoutedEventHandler? BackRequested;

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, e);
        }
    }
}
