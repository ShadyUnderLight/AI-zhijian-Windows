using System.Windows;

namespace AIZhijian;

public partial class App : Application
{
    public static readonly Services.ApiService Api = Services.ApiService.Instance;
}
