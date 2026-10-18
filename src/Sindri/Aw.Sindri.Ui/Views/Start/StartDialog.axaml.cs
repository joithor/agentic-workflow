using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Start;

namespace Aw.Sindri.Ui.Views.Start;

/// <summary>The Start workflow form; Ctrl+Enter (⌘+Enter on macOS) sends, Esc and Cancel close it with nothing sent.</summary>
internal sealed partial class StartDialog : UserControl
{
    private StartFormViewModel? _form;

    public StartDialog()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnAnyClick, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnSendChord, RoutingStrategies.Tunnel);   // before a focused combo box can use Enter
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_form is { } old)
        {
            old.PropertyChanged -= OnForm;
        }

        _form = DataContext as StartFormViewModel;
        if (_form is { } form)
        {
            form.PropertyChanged += OnForm;
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>Moves focus to the recipe picker (the form just opened).</summary>
    internal void FocusFirst() => this.Get<ComboBox>("StartRecipe").Focus(NavigationMethod.Tab);

    private void OnSendChord(object? sender, KeyEventArgs e)
    {
        if (DataContext is StartFormViewModel form && e.Key == Key.Enter && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != KeyModifiers.None)
        {
            _ = form.Submit(CancellationToken.None);
            e.Handled = true;
        }
    }

    // A refusal or a field that cannot be sent asks for focus: on the control the error is about, or on the error line for the API's own refusal.
    private void OnForm(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(StartFormViewModel.ErrorFocusRequests), StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(FocusError, DispatcherPriority.Loaded);
        }
        else if (string.Equals(e.PropertyName, nameof(StartFormViewModel.OutcomeFocusRequests), StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(FocusOutcome, DispatcherPriority.Loaded);
        }
    }

    private void FocusError()
    {
        if (DataContext is not StartFormViewModel form)
        {
            return;
        }

        var target = form.ErrorTarget == StartErrorTarget.Field ? FieldControl(form.ErrorField) : null;
        target ??= form.ErrorTarget switch
        {
            StartErrorTarget.Recipe => this.Get<ComboBox>("StartRecipe"),
            StartErrorTarget.Item => this.Get<TextBox>("StartItem"),
            StartErrorTarget.Field or StartErrorTarget.Summary => this.Get<Border>("StartError"),
            _ => this.Get<Border>("StartError"),
        };
        _ = target.Focus(NavigationMethod.Tab);
    }

    // An existing attempt was answered: the Start button just pressed is off, so focus goes to the new-attempt button when it is offered, else the outcome line.
    private void FocusOutcome()
    {
        Control target = DataContext is StartFormViewModel { OffersNewAttempt: true } ? this.Get<Button>("StartNewAttempt") : this.Get<Border>("StartOutcome");
        _ = target.Focus(NavigationMethod.Tab);
    }

    // The control of an input: its visible box, or its file button.
    private Control? FieldControl(StartFieldViewModel? field) =>
        field is null
            ? null
            : this.Get<ItemsControl>("StartFields").GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(c => ReferenceEquals(c.DataContext, field) && c.IsEffectivelyVisible && ((c is TextBox && c.Classes.Contains("start-input")) || (c is Button && c.Classes.Contains("start-pick"))));

    private void OnRecipeChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is StartFormViewModel form && e.AddedItems is [RecipeChoice choice, ..])
        {
            _ = form.Choose(choice, CancellationToken.None);
        }
    }

    // The file buttons live in the fields' template: one handler here finds the field they belong to.
    private static void OnAnyClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { DataContext: StartFieldViewModel field } button && button.Classes.Contains("start-pick"))
        {
            _ = field.Pick(CancellationToken.None);
        }
    }

    private void OnStart(object? sender, RoutedEventArgs e) => _ = (DataContext as StartFormViewModel)?.Submit(CancellationToken.None);

    private void OnNewAttempt(object? sender, RoutedEventArgs e) => _ = (DataContext as StartFormViewModel)?.StartNewAttempt(CancellationToken.None);

    private void OnCancel(object? sender, RoutedEventArgs e) => (DataContext as StartFormViewModel)?.Close();

    private void OnScrim(object? sender, PointerPressedEventArgs e) => (DataContext as StartFormViewModel)?.Close();
}
