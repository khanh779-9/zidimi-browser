namespace Zidimi.Browser.Controls;

/// <summary>
/// Helper for adding items to a <see cref="ZidimiComboBox"/> from code-behind,
/// mirroring the raw <c>ComboBoxItem</c>: it has a Content and an IsSelected property.
/// </summary>
public sealed class ZidimiComboBoxItem
{
    public object? Content { get; set; }

    public bool IsSelected { get; set; }

    public override string ToString() => Content?.ToString() ?? "";
}
