namespace AgentSprint.Model.Modules.Security.Dtos;

public sealed class MenuResult
{
    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string? Component { get; set; }

    public string? Redirect { get; set; }

    public MenuMetaResult Meta { get; set; } = new();

    public List<MenuResult> Children { get; set; } = [];
}

public sealed class MenuMetaResult
{
    public string Title { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public int? Order { get; set; }

    public bool? AffixTab { get; set; }

    /// <summary>
    /// zh-cn: 指示动态路由是否应从管理端左侧菜单中隐藏；详情页等可访问但不作为菜单入口的路由应设置为 true。
    /// en-us: Indicates whether the dynamic route should be hidden from the admin sidebar menu; routes such as detail pages that remain accessible without becoming menu entries should set this value to true.
    /// </summary>
    public bool? HideInMenu { get; set; }

    /// <summary>
    /// zh-cn: 指定当前路由访问时应高亮的菜单路径，通常用于详情页保持其列表菜单处于选中状态。
    /// en-us: Specifies the menu path that should stay active while this route is visited, usually allowing detail pages to keep their list menu selected.
    /// </summary>
    public string? ActivePath { get; set; }
}
