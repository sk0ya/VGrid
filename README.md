# VGrid - WPF TSV Editor with Vim Keybindings

VimキーバインドをサポートするTSV（Tab-Separated Values）エディタです。

## 機能

### 実装済み (Phase 1-9)

- **ファイル操作**
  - TSVファイルの開く/保存
  - 新規ファイル作成

- **Vimモーダル編集**
  - Normal Mode: ナビゲーション用
  - Insert Mode: テキスト編集用
  - Visual Mode: 選択用

- **基本的なナビゲーション (Normal Mode)**
  - `h/j/k/l`: 左/下/上/右に移動
  - `0`: 行頭に移動
  - `$` (Shift+4): 行末に移動
  - `gg`: ファイルの先頭に移動
  - カウントプレフィックス: `3j`で3行下に移動など

- **モード切替**
  - `i`: Insert Modeに切替
  - `a`: カーソルの右でInsert Modeに切替
  - `o`: 下に新しい行を挿入してInsert Modeに切替
  - `v`: Visual Modeに切替
  - `Esc`: Normal Modeに戻る

- **Insert Mode**
  - 通常のテキスト入力
  - 矢印キーでのナビゲーション
  - `Esc`でNormal Modeに戻る

- **Visual Mode**
  - `h/j/k/l`: 選択範囲を拡張
  - `Esc`: Normal Modeに戻る

- **UI機能**
  - ステータスバーに現在のモードとカーソル位置を表示
  - モードごとに色分け（Normal: 青、Insert: 緑、Visual: オレンジ）
  - メニューバーからのファイル操作

### 未実装 (Phase 10-20)

今後実装予定の機能:
- 単語移動（`w`, `b`, `e`）
- 検索・置換（`/`, `?`, `:%s/old/new/g`）
- ソート・フィルタ
- コマンドモード（`:w`, `:q`, `:wq`）
- Undo/Redo統合
- git連携 差分の表示
- vimのカスタム

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
2. `File > Open...`から`sample.tsv`を開く（またはメニューから新規作成）
3. デフォルトでNormal Modeで起動
4. `h/j/k/l`でカーソル移動
5. `i`でInsert Modeに入り、セルを編集
6. `Esc`でNormal Modeに戻る
7. `File > Save`で保存（または`:w`コマンド - 未実装）

## キーバインド一覧

### Normal Mode
| キー | 動作 |
|------|------|
| h | 左に移動 |
| j | 下に移動 |
| k | 上に移動 |
| l | 右に移動 |
| 0 | 行頭に移動 |
| $ | 行末に移動 |
| gg | ファイルの先頭に移動 |
| i | Insert Modeに切替 |
| a | カーソルの右でInsert Mode |
| o | 下に行挿入してInsert Mode |
| v | Visual Modeに切替 |
| {数字}{コマンド} | コマンドを指定回数実行（例: 3j） |

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
| Esc | Normal Modeに戻る |

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
- ⏳ Phase 10-14: Vim機能拡張と検索
- ⏳ Phase 15-18: コマンドモードとファイル操作
- ⏳ Phase 19-20: 最終調整とテスト
