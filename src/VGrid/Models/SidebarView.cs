namespace VGrid.Models;

/// <summary>
/// サイドバーに表示するビューの種類
/// </summary>
public enum SidebarView
{
    /// <summary>
    /// ファイルエクスプローラー
    /// </summary>
    Explorer,

    /// <summary>
    /// Git変更
    /// </summary>
    GitChanges,

    /// <summary>
    /// テンプレート
    /// </summary>
    Template,

    /// <summary>
    /// 設定
    /// </summary>
    Settings
}
