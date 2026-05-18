using AIZhijian.Services;

namespace AIZhijian.ViewModels;

public class MainViewModel
{
    private readonly ApiService _api = ApiService.Instance;
    public event Action? StateChanged;

    public async Task Initialize()
    {
        _api.StateChanged += () => StateChanged?.Invoke();
        await _api.CheckSessionStatus();
    }

    public async Task Logout()
    {
        await _api.Logout();
    }
}
