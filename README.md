# VGrid - WPF TSV Editor with Vim Keybindings

VimキーバインドをサポートするTSV（Tab-Separated Values）エディタです。

## 機能

### 実装済み (Phase 1-14)

- **ファイル操作**
  - TSVファイルの開く/保存/新規作成
  - 複数タブ対応
  - `:w`, `:q`, `:wq`, `:x` コマンド

- **Vimモーダル編集**
  - Normal Mode: ナビゲーション用
  - Insert Mode: テキスト編集用
  - Visual Mode: 選択用 (Character / Line / Block)
  - Command Mode: 検索とExコマンド用

- **基本的なナビゲーション (Normal Mode)**
  - `h/j/k/l`: 左/下/上/右に移動
  - `0`: 行頭（最初の列）に移動
  - `H` (Shift+H): 行頭に移動
  - `$`: 行末に移動
  - `L` (Shift+L): 最後の非空列に移動
  - `gg`: ファイルの先頭に移動
  - `w`: 次の非空セルに移動
  - `b`: 前の非空セルに移動
  - カウントプレフィックス: `3j`で3行下に移動など

- **モード切替**
  - `i`: Insert Modeに切替（カーソル位置から）
  - `I` (Shift+I): 左セルに移動してInsert Mode
  - `a`: カーソルの右でInsert Modeに切替
  - `A` (Shift+A): 右セルに移動してInsert Mode
  - `o`: 下に新しい行を挿入してInsert Mode
  - `O` (Shift+O): 上に新しい行を挿入してInsert Mode
  - `v`: Visual Mode (Character-wise)
  - `V` (Shift+V): Visual Mode (Line-wise)
  - `Ctrl+V`: Visual Mode (Block-wise)
  - `Esc`: Normal Modeに戻る

- **Yank (コピー) 操作**
  - `yy`: 現在行をyank
  - `yiw`, `yaw`: 現在セルをyank
  - `Ctrl+C`: 現在セルをyank
  - Visual Modeで `y` または `Ctrl+C`: 選択範囲をyank

- **Paste (貼り付け) 操作**
  - `p`: yankした内容を貼り付け
  - `Ctrl+V`: yankした内容を貼り付け
  - Line-wise: 新しい行として挿入
  - Block-wise: 新しい列として挿入
  - Character-wise: セルを上書き

- **Delete (削除) 操作**
  - `dd`: 現在行を削除（yankも同時に実行）
  - `x`: 現在セルを削除（yankも同時に実行）
  - `diw`, `daw`: 現在セルを削除
  - Visual Modeで `d`: 選択範囲を削除

- **検索機能**
  - `/pattern`: 検索（正規表現対応）
  - `n`: 次の検索結果に移動
  - `N` (Shift+N): 前の検索結果に移動
  - `:pattern`: Exコマンド入力

- **Undo/Redo**
  - `u`: 最後の変更を取り消し
  - Command Patternによる履歴管理（最大100コマンド）

- **Insert Mode**
  - 通常のテキスト入力
  - 矢印キーでのナビゲーション
  - `Esc`でNormal Modeに戻る
  - Visual Modeからの一括編集（`i`/`a`で選択範囲を編集）

- **Visual Mode**
  - `h/j/k/l`: 選択範囲を拡張
  - `H/L`: 行頭/最終非空列まで拡張
  - `w/b`: 次/前の非空セルまで拡張
  - `y`: 選択範囲をyank
  - `d`: 選択範囲を削除
  - `i`/`a`: 選択範囲を一括編集
  - `Esc`: Normal Modeに戻る

- **UI機能**
  - ステータスバーに現在のモードとカーソル位置を表示
  - モードごとに色分け（Normal: 青、Insert: 緑、Visual: オレンジ、Command: 紫）
  - メニューバーからのファイル操作
  - セル選択の視覚的フィードバック

- **リーダーキー (Space)**
  - `Space w`: ファイルを保存

### 今後の拡張予定

- ソート・フィルタ機能
- Git連携（差分表示）
- Vimキーバインドのカスタマイズ
- マクロ記録・再生

## ビルドと実行

### 必要な環境
- .NET 8 SDK
- Windows (WPFアプリケーション)

### ビルド
```bash
cd C:\Projects\VGrid
dotnet build
```

### 実行
```bash
cd C:\Projects\VGrid\src\VGrid
dotnet run
```

または、Visual Studioで`VGrid.sln`を開いて実行。

## 使い方

1. アプリケーションを起動
2. `File > Open...`からTSVファイルを開く（または`Ctrl+O`）
3. デフォルトでNormal Modeで起動
4. `h/j/k/l`でカーソル移動
5. `i`でInsert Modeに入り、セルを編集
6. `Esc`でNormal Modeに戻る
7. `:w`で保存（または`Space w`、`Ctrl+S`）
8. `:q`でタブを閉じる、`:wq`で保存して閉じる

### 基本的なワークフロー例

**データ編集:**
- `gg`でファイル先頭に移動
- `3j`で3行下に移動
- `i`でInsert Modeに入りセルを編集
- `Esc`でNormal Modeに戻る

**行のコピー&ペースト:**
- `yy`で現在行をコピー
- `5j`で5行下に移動
- `p`で貼り付け

**範囲選択と一括編集:**
- `v`でVisual Modeに入る
- `3j2l`で範囲を選択
- `i`で選択範囲を一括編集

**検索:**
- `/TODO`で「TODO」を検索
- `n`で次の結果に移動
- `N`で前の結果に移動

## キーバインド一覧

### Normal Mode
| キー | 動作 |
|------|------|
| **移動** | |
| h | 左に移動 |
| j | 下に移動 |
| k | 上に移動 |
| l | 右に移動 |
| 0 | 行頭（最初の列）に移動 |
| H (Shift+H) | 行頭に移動 |
| $ (Shift+4) | 行末に移動 |
| L (Shift+L) | 最後の非空列に移動 |
| gg | ファイルの先頭に移動 |
| w | 次の非空セルに移動 |
| b | 前の非空セルに移動 |
| {数字}{コマンド} | コマンドを指定回数実行（例: 3j） |
| **モード切替** | |
| i | Insert Modeに切替 |
| I (Shift+I) | 左セルに移動してInsert Mode |
| a | カーソルの右でInsert Mode |
| A (Shift+A) | 右セルに移動してInsert Mode |
| o | 下に行挿入してInsert Mode |
| O (Shift+O) | 上に行挿入してInsert Mode |
| v | Visual Mode (Character-wise) |
| V (Shift+V) | Visual Mode (Line-wise) |
| Ctrl+V | Visual Mode (Block-wise) |
| **Yank/Paste** | |
| yy | 現在行をyank |
| yiw, yaw | 現在セルをyank |
| Ctrl+C | 現在セルをyank |
| p | yankした内容を貼り付け |
| Ctrl+V | yankした内容を貼り付け |
| **削除** | |
| dd | 現在行を削除 |
| x | 現在セルを削除 |
| diw, daw | 現在セルを削除 |
| **検索** | |
| / | 検索モードに入る |
| n | 次の検索結果に移動 |
| N (Shift+N) | 前の検索結果に移動 |
| **Exコマンド** | |
| : | コマンドモードに入る |
| **Undo** | |
| u | 最後の変更を取り消し |
| **リーダーキー** | |
| Space w | ファイルを保存 |

### Insert Mode
| キー | 動作 |
|------|------|
| Esc | Normal Modeに戻る |
| 矢印キー | カーソル移動 |
| 文字入力 | テキスト編集 |

### Visual Mode
| キー | 動作 |
|------|------|
| h/j/k/l | 選択範囲を拡張 |
| H/L | 行頭/最終非空列まで拡張 |
| w/b | 次/前の非空セルまで拡張 |
| y, Ctrl+C | 選択範囲をyank |
| d | 選択範囲を削除 |
| i | 選択範囲を一括編集（カーソル位置から） |
| a | 選択範囲を一括編集（セル末尾から） |
| Esc | Normal Modeに戻る |

### Command Mode
| コマンド | 動作 |
|---------|------|
| :w | ファイルを保存 |
| :q | タブを閉じる |
| :q! | 変更を破棄してタブを閉じる |
| :wq, :x | 保存して閉じる |
| /pattern | パターンで検索（正規表現対応） |

## プロジェクト構造

```
VGrid/
├── src/VGrid/
│   ├── Models/          # データモデル
│   ├── ViewModels/      # MVVM ViewModels
│   ├── VimEngine/       # Vimエンジン
│   ├── Commands/        # Undo/Redoコマンドパターン
│   ├── Services/        # ファイルI/Oサービス
│   ├── Helpers/         # ヘルパークラス
│   └── Converters/      # WPF Converters
└── tests/VGrid.Tests/   # ユニットテスト
```

## アーキテクチャ

- **MVVM**: WPFベストプラクティス
- **State Pattern**: Vimモード管理
- **Command Pattern**: Undo/Redo対応
- **Observer Pattern**: データバインディング

## ライセンス

このプロジェクトはサンプル実装です。

## 開発状況

- ✅ Phase 1-9: 基礎実装とUI完了
- ✅ Phase 10-14: Vim機能拡張と検索完了
  - Yank/Paste操作 (yy, p, yiw, daw など)
  - Word movement (w, b)
  - 検索機能 (/, n, N)
  - Exコマンド (:w, :q, :wq)
- ⏳ Phase 15-18: 高度な機能
  - ソート・フィルタ操作
  - マクロ記録・再生
  - 設定のカスタマイズ
- ⏳ Phase 19-20: 最終調整とテスト
