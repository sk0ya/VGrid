using System.IO;
using VGrid.VimEngine.KeyBinding;
using VGrid.VimEngine.Vimrc;

namespace VGrid.Services;

/// <summary>
/// Service for loading and managing vimrc configuration
/// </summary>
public class VimrcService : IVimrcService
{
    private readonly VimrcParser _parser = new();
    private readonly List<string> _loadErrors = new();

    /// <inheritdoc />
    public KeyBindingConfig Config { get; private set; } = new();

    /// <inheritdoc />
    public bool IsLoaded { get; private set; }

    /// <inheritdoc />
    public string VimrcPath { get; private set; } = string.Empty;

    /// <inheritdoc />
    public IReadOnlyList<string> LoadErrors => _loadErrors.AsReadOnly();

    /// <summary>
    /// Gets the default vimrc path
    /// </summary>
    public static string DefaultVimrcPath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "VGrid", ".vimrc");
        }
    }

    /// <inheritdoc />
    public void Load()
    {
        Load(DefaultVimrcPath);
    }

    /// <inheritdoc />
    public void Load(string path)
    {
        VimrcPath = path;
        _loadErrors.Clear();
        Config = new KeyBindingConfig();
        IsLoaded = false;

        try
        {
            if (!File.Exists(path))
            {
                // No vimrc file - use defaults (empty config)
                System.Diagnostics.Debug.WriteLine($"[VimrcService] No vimrc file at: {path}");
                return;
            }

            var content = File.ReadAllText(path);
            var result = _parser.Parse(content);

            if (result.HasErrors)
            {
                foreach (var error in result.Errors)
                {
                    _loadErrors.Add($"Line {error.LineNumber}: {error.Message}");
                    System.Diagnostics.Debug.WriteLine($"[VimrcService] Parse error at line {error.LineNumber}: {error.Message}");
                }
            }

            Config = result.Config;
            IsLoaded = true;

            System.Diagnostics.Debug.WriteLine($"[VimrcService] Loaded {Config.TotalBindingCount} keybindings from: {path}");
        }
        catch (Exception ex)
        {
            _loadErrors.Add($"Failed to load vimrc: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[VimrcService] Error loading vimrc: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void Reload()
    {
        if (!string.IsNullOrEmpty(VimrcPath))
        {
            Load(VimrcPath);
        }
        else
        {
            Load();
        }
    }

    /// <inheritdoc />
    public bool CreateDefaultIfNotExists()
    {
        var path = DefaultVimrcPath;

        if (File.Exists(path))
            return false;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var defaultContent = GetDefaultVimrcContent();
            File.WriteAllText(path, defaultContent);

            System.Diagnostics.Debug.WriteLine($"[VimrcService] Created default vimrc at: {path}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VimrcService] Failed to create default vimrc: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the default vimrc content with example mappings
    /// </summary>
    private static string GetDefaultVimrcContent()
    {
        return @""" VGrid .vimrc configuration file
"" This file contains custom keybindings for VGrid

"" Syntax:
""   nmap <key> <action>   - Normal mode mapping
""   imap <key> <action>   - Insert mode mapping
""   vmap <key> <action>   - Visual mode mapping

"" Available key notations:
""   <C-x>   - Ctrl+x
""   <S-x>   - Shift+x
""   <A-x>   - Alt+x
""   <Space> - Space key
""   <CR>    - Enter key
""   <Esc>   - Escape key
""   <Tab>   - Tab key
""   <BS>    - Backspace key

"" Example mappings (uncomment to enable):
"" nmap <C-j> move_down_10       "" Ctrl+j moves down 10 rows
"" nmap <C-k> move_up_10         "" Ctrl+k moves up 10 rows
"" nmap <Space>w save_file       "" Space+w saves the file
"" nmap <Space>q quit            "" Space+q quits

"" Available actions:
"" Movement:
""   move_left, move_right, move_up, move_down
""   move_up_10, move_down_10
""   move_to_line_start, move_to_first_cell, move_to_last_column
""   move_to_first_line, move_to_last_line
""   move_to_next_word, move_to_prev_word
""   move_to_next_empty_row, move_to_prev_empty_row
""
"" Edit:
""   delete_line, delete_cell, delete_word, delete_selection
""   yank_line, yank_cell, yank_word, yank_selection
""   paste_after, paste_before
""   undo, redo
""   align_selection
""   change_line, change_word
""
"" Mode:
""   switch_to_insert, switch_to_insert_line_start
""   switch_to_append, switch_to_append_line_end
""   switch_to_insert_below, switch_to_insert_above
""   switch_to_visual, switch_to_visual_line, switch_to_visual_block
""   switch_to_command, start_search
""   switch_to_normal
""
"" File:
""   save_file, quit, force_quit, save_and_quit
""
"" Tab:
""   switch_to_prev_tab, switch_to_next_tab
""
"" Scroll:
""   scroll_to_center
""
"" Search:
""   navigate_to_next_match, navigate_to_prev_match
";
    }
}
