namespace Flowery.NET.Kanban.Controls
{
    // A board lower than ShortHeight, such as on a phone on its side, gives its height to the cards: the search moves
    // beside the title (the :short styles of the theme), the status bar shows only a status message, and the columns
    // keep their headers and Add Card flatter.
    public partial class FlowKanban
    {
        internal const double ShortHeight = 480;

        internal bool IsShort { get; private set; }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            var isShort = e.NewSize.Height < ShortHeight;
            if (isShort == IsShort)
                return;

            IsShort = isShort;
            PseudoClasses.Set(":short", isShort);
            foreach (var column in _cachedColumnControls)
                column.SetShort(isShort);
            UpdateBoardStatusBarVisibility();
        }
    }
}
