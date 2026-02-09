using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StreamTalkerClient.Components;

public partial class Placeholder : UserControl
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<Placeholder, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<Placeholder, string>(nameof(Text), "");

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Placeholder()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty)
        {
            var icon = this.FindControl<PathIcon>("IconElement");
            if (icon != null)
                icon.Data = change.GetNewValue<Geometry?>();
        }
        else if (change.Property == TextProperty)
        {
            var text = this.FindControl<TextBlock>("TextElement");
            if (text != null)
                text.Text = change.GetNewValue<string>();
        }
    }
}
