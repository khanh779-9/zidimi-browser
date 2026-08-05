namespace Heco.Browser.Controls;

/// <summary>
/// Helper để thêm item vào <see cref="HecoComboBox"/> từ code-behind,
/// giống <c>ComboBoxItem</c> gốc: có Content và IsSelected.
/// </summary>
public sealed class HecoComboBoxItem
{
    public object? Content { get; set; }

    public bool IsSelected { get; set; }

    public override string ToString() => Content?.ToString() ?? "";
}
