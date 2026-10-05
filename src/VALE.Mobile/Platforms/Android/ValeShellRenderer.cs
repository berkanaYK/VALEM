using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace VALE.Mobile;

public sealed class ValeShellRenderer : ShellRenderer
{
    protected override IShellItemRenderer CreateShellItemRenderer(ShellItem shellItem) => new RootTabRenderer(this);

    private sealed class RootTabRenderer(IShellContext context) : ShellItemRenderer(context)
    {
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
