using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

/// <summary>
/// Keeps the operational branch choice small and consistent across pages.
/// It stays completely hidden for users who can access only one branch.
/// </summary>
public sealed class BranchContextSelector : ContentView
{
    private readonly ApiClient _api;
    private readonly Func<Task>? _onChanged;
    private readonly Picker _picker = UiKit.Picker("Çalışılan şube");
    private IReadOnlyList<BranchDto> _branches = Array.Empty<BranchDto>();
    private Task? _loadTask;
    private bool _suppressSelection;

    public BranchContextSelector(ApiClient api, Func<Task>? onChanged = null)
    {
        _api = api;
        _onChanged = onChanged;
        IsVisible = false;
        Content = UiKit.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                UiKit.Label("Çalışılan şube", 10.5, true, true),
                _picker,
                UiKit.Label("Bu seçim ana sayfa, araç listesi, yeni kabul ve raporlarda birlikte kullanılır.", 10.5, false, true)
            }
        }, new Thickness(12), 14);
        _picker.SelectedIndexChanged += PickerOnSelectedIndexChanged;
        Loaded += async (_, _) => await EnsureLoadedAsync();
    }

    public async Task EnsureLoadedAsync()
    {
        _loadTask ??= LoadAsync();
        await _loadTask;
        SyncSelection();
    }

    private async Task LoadAsync()
    {
        try
        {
            _branches = await _api.EnsureBranchContextAsync();
            _picker.ItemsSource = _branches.Select(x => $"{x.Code} • {x.Name}").ToList();
            IsVisible = _branches.Count > 1;
            SyncSelection();
        }
        catch
        {
            // Branch-specific API calls still safely fall back to the branch in the signed token.
            IsVisible = false;
        }
    }

    private void SyncSelection()
    {
        if (_branches.Count == 0) return;
        var index = _api.ActiveBranchId.HasValue
            ? _branches.ToList().FindIndex(x => x.Id == _api.ActiveBranchId.Value)
            : -1;
        if (index < 0) index = 0;
        _suppressSelection = true;
        _picker.SelectedIndex = index;
        _suppressSelection = false;
    }

    private async void PickerOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressSelection || _picker.SelectedIndex < 0 || _picker.SelectedIndex >= _branches.Count) return;
        var changed = _api.ActiveBranchId != _branches[_picker.SelectedIndex].Id;
        if (!changed || !_api.SelectActiveBranch(_branches[_picker.SelectedIndex].Id)) return;
        if (_onChanged is not null) await _onChanged();
    }
}
