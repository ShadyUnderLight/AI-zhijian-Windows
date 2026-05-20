using System.Windows;

namespace AIZhijian.Views;

public partial class TextInputDialog : Window
{
    public string? Answer => DialogResult == true ? InputBox.Text.Trim() : null;

    public TextInputDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        InputBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text))
        {
            MessageBox.Show("请输入名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }
}
