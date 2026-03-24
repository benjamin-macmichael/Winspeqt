using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Windows.Input;

namespace Winspeqt.Controls
{
    public sealed partial class StandardPageHeader : UserControl
    {
        public StandardPageHeader()
        {
            this.InitializeComponent();
        }

        public string BackText
        {
            get => (string)GetValue(BackTextProperty);
            set => SetValue(BackTextProperty, value);
        }

        public static readonly DependencyProperty BackTextProperty =
            DependencyProperty.Register(nameof(BackText), typeof(string), typeof(StandardPageHeader), new PropertyMetadata("Back"));

        public string IconGlyph
        {
            get => (string)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(StandardPageHeader), new PropertyMetadata(string.Empty, OnIconGlyphChanged));

        public Brush? IconBrush
        {
            get => (Brush?)GetValue(IconBrushProperty);
            set => SetValue(IconBrushProperty, value);
        }

        public static readonly DependencyProperty IconBrushProperty =
            DependencyProperty.Register(nameof(IconBrush), typeof(Brush), typeof(StandardPageHeader), new PropertyMetadata(null));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(StandardPageHeader), new PropertyMetadata(string.Empty));

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(StandardPageHeader), new PropertyMetadata(string.Empty));

        public ICommand? ActionCommand
        {
            get => (ICommand?)GetValue(ActionCommandProperty);
            set => SetValue(ActionCommandProperty, value);
        }

        public static readonly DependencyProperty ActionCommandProperty =
            DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(StandardPageHeader), new PropertyMetadata(null));

        public string ActionText
        {
            get => (string)GetValue(ActionTextProperty);
            set => SetValue(ActionTextProperty, value);
        }

        public static readonly DependencyProperty ActionTextProperty =
            DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(StandardPageHeader), new PropertyMetadata(string.Empty));

        public string ActionGlyph
        {
            get => (string)GetValue(ActionGlyphProperty);
            set => SetValue(ActionGlyphProperty, value);
        }

        public static readonly DependencyProperty ActionGlyphProperty =
            DependencyProperty.Register(nameof(ActionGlyph), typeof(string), typeof(StandardPageHeader), new PropertyMetadata(string.Empty, OnActionGlyphChanged));

        public Visibility ActionVisibility
        {
            get => (Visibility)GetValue(ActionVisibilityProperty);
            set => SetValue(ActionVisibilityProperty, value);
        }

        public static readonly DependencyProperty ActionVisibilityProperty =
            DependencyProperty.Register(nameof(ActionVisibility), typeof(Visibility), typeof(StandardPageHeader), new PropertyMetadata(Visibility.Collapsed));

        public Thickness HeaderMargin
        {
            get => (Thickness)GetValue(HeaderMarginProperty);
            set => SetValue(HeaderMarginProperty, value);
        }

        public static readonly DependencyProperty HeaderMarginProperty =
            DependencyProperty.Register(nameof(HeaderMargin), typeof(Thickness), typeof(StandardPageHeader), new PropertyMetadata(new Thickness(0, 0, 0, 32)));

        public Visibility IconVisibility
        {
            get => (Visibility)GetValue(IconVisibilityProperty);
            private set => SetValue(IconVisibilityProperty, value);
        }

        public static readonly DependencyProperty IconVisibilityProperty =
            DependencyProperty.Register(nameof(IconVisibility), typeof(Visibility), typeof(StandardPageHeader), new PropertyMetadata(Visibility.Collapsed));

        public Visibility ActionGlyphVisibility
        {
            get => (Visibility)GetValue(ActionGlyphVisibilityProperty);
            private set => SetValue(ActionGlyphVisibilityProperty, value);
        }

        public static readonly DependencyProperty ActionGlyphVisibilityProperty =
            DependencyProperty.Register(nameof(ActionGlyphVisibility), typeof(Visibility), typeof(StandardPageHeader), new PropertyMetadata(Visibility.Collapsed));

        public event RoutedEventHandler? BackRequested;

        private static void OnIconGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StandardPageHeader header)
            {
                header.IconVisibility = string.IsNullOrWhiteSpace(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private static void OnActionGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StandardPageHeader header)
            {
                header.ActionGlyphVisibility = string.IsNullOrWhiteSpace(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, e);
        }
    }
}
