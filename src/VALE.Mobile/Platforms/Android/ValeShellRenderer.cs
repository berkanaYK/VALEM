using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace VALE.Mobile;

public sealed class ValeShellRenderer : ShellRenderer
{
    protected override IShellItemRenderer CreateShellItemRenderer(ShellItem shellItem) => new RootTabRenderer(this);

    private sealed class RootTabRenderer(IShellContext context) : ShellItemRenderer(context)
    {
        private Android.Views.View? _tabRoot;

        public override Android.Views.View OnCreateView(Android.Views.LayoutInflater inflater, Android.Views.ViewGroup? container, Android.OS.Bundle? savedInstanceState)
        {
            _tabRoot = base.OnCreateView(inflater, container, savedInstanceState);
            FormatTabLabels(_tabRoot);
            return _tabRoot;
        }

        protected override void OnShellSectionChanged()
        {
            base.OnShellSectionChanged();
            if (_tabRoot is not null) FormatTabLabels(_tabRoot);
        }

        private static void FormatTabLabels(Android.Views.View view, bool insideBottomNavigation = false)
        {
            if (view is Google.Android.Material.BottomNavigation.BottomNavigationView)
            {
                insideBottomNavigation = true;
            }
            if (insideBottomNavigation && view is Android.Widget.TextView label && label.Id > 0)
            {
                var id = view.Resources?.GetResourceEntryName(label.Id);
                if (id is "navigation_bar_item_small_label_view" or "navigation_bar_item_large_label_view")
                {
                    label.SetSingleLine(true);
                    label.SetMaxLines(1);
                    label.SetIncludeFontPadding(false);
                    label.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                    label.Gravity = Android.Views.GravityFlags.Center;
                    label.SetTextSize(Android.Util.ComplexUnitType.Sp, 10);
                    label.Text = label.Text?.Replace("\n", " ");
                }
            }
            if (view is Android.Views.ViewGroup group)
                for (var i = 0; i < group.ChildCount; i++)
                    if (group.GetChildAt(i) is { } child) FormatTabLabels(child, insideBottomNavigation);
        }

        protected override void OnTabReselected(ShellSection section)
        {
            section.Dispatcher.Dispatch(async () =>
            {
                if (section.Navigation.NavigationStack.Count > 1)
                    await section.Navigation.PopToRootAsync(false);
            });
        }
    }
}
