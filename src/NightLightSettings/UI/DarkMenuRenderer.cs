namespace NightLightSettings.UI;

/// <summary>暗色右键菜单。</summary>
internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.BackgroundElevated;
    public override Color MenuBorder => Theme.Border;
    public override Color MenuItemBorder => Color.FromArgb(48, 48, 54);
    public override Color MenuItemSelected => Color.FromArgb(46, 46, 52);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(46, 46, 52);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(46, 46, 52);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(40, 40, 46);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(40, 40, 46);
    public override Color ImageMarginGradientBegin => Theme.BackgroundElevated;
    public override Color ImageMarginGradientMiddle => Theme.BackgroundElevated;
    public override Color ImageMarginGradientEnd => Theme.BackgroundElevated;
    public override Color SeparatorDark => Theme.Border;
    public override Color SeparatorLight => Theme.BackgroundElevated;
    public override Color CheckBackground => Color.FromArgb(58, 58, 66);
    public override Color CheckSelectedBackground => Color.FromArgb(58, 58, 66);
    public override Color CheckPressedBackground => Color.FromArgb(58, 58, 66);
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.TextFaint;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.TextDim;
        base.OnRenderArrow(e);
    }
}
